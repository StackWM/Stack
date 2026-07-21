/* global workspace, options, readConfig, registerShortcut, registerDBusService, registerDBusObject */

// KWin "Stack" prototype:
// - zone layouts (drop zones -> target zones)
// - stacking / simple tiling per zone
// - outline overlay while moving windows (when available)
// - DBus interface for CLI integration

  (function () {
    "use strict";

    function log(msg) {
      // KWin scripts typically route print() to KWin logs.
      try {
        print("[stack] " + msg);
      } catch (e) {
        // ignore
      }
    }

    var _warnOnceSeen = {};
    var _warnOnceCount = 0;
    var _warnOnceMax = 200;

    function errToString(e) {
      try {
        if (!e) return "unknown";
        if (e.stack) return "" + e.stack;
        if (e.message) return "" + e.message;
        return "" + e;
      } catch (e2) {
        return "unknown";
      }
    }

    function truncate(s, maxLen) {
      try {
        var str = "" + s;
        if (!maxLen || maxLen <= 0) return str;
        if (str.length <= maxLen) return str;
        return str.substring(0, maxLen) + "...";
      } catch (e) {
        return "";
      }
    }

    function warn(msg) {
      log("WARN " + msg);
    }

    function warnOnce(key, msg, e) {
      try {
        var k = "" + key;
        if (_warnOnceSeen[k]) return;
        if (_warnOnceCount >= _warnOnceMax) return;
        _warnOnceSeen[k] = true;
        _warnOnceCount++;
        var full = msg;
        if (e) full = full + " (" + truncate(errToString(e), 400) + ")";
        warn(full);
      } catch (e2) {
        // ignore (we don't want warning emission to crash the script)
      }
    }

    function safeStringify(obj) {
      try {
        return JSON.stringify(obj);
      } catch (e) {
        return "" + obj;
    }
  }

  function isFn(f) {
    return typeof f === "function";
  }

  function rect(x, y, w, h) {
    // KWin script bindings generally accept plain objects as QRect.
    return { x: x, y: y, width: w, height: h };
  }

  function rectToObj(r) {
    if (!r) return null;
    return {
      x: r.x,
      y: r.y,
      width: r.width,
      height: r.height,
    };
  }

  function rectCenter(r) {
    return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
  }

  function rectIntersect(a, b) {
    if (!a || !b) return a || b;
    var x1 = Math.max(a.x, b.x);
    var y1 = Math.max(a.y, b.y);
    var x2 = Math.min(a.x + a.width, b.x + b.width);
    var y2 = Math.min(a.y + a.height, b.y + b.height);
    var w = x2 - x1;
    var h = y2 - y1;
    if (w <= 0 || h <= 0) return a;
    return rect(x1, y1, w, h);
  }

  function pickSmallerArea(a, b) {
    if (!a) return b;
    if (!b) return a;
    try {
      var av = (a.width || 0) * (a.height || 0);
      var bv = (b.width || 0) * (b.height || 0);
      if (bv > 0 && (av <= 0 || bv < av)) return b;
    } catch (e) {}
    return a;
  }

  function pointInRect(p, r) {
    return p.x >= r.x && p.x <= r.x + r.width && p.y >= r.y && p.y <= r.y + r.height;
  }

  function clamp(v, minV, maxV) {
    return Math.max(minV, Math.min(maxV, v));
  }

  function dist2(a, b) {
    var dx = a.x - b.x;
    var dy = a.y - b.y;
    return dx * dx + dy * dy;
  }

  function isManageableClient(c) {
    if (!c) return false;
    // KWin provides many flags; keep this permissive but avoid obvious non-windows.
    if (c.deleted) return false;
    if (c.specialWindow) return false;
    if (c.dock) return false;
    if (c.desktopWindow) return false;
    if (c.splash) return false;
    if (c.toolbar) return false;
    if (c.menu) return false;
    if (c.utility) return false;
    if (c.popupWindow) return false;
    if (c.notification) return false;
    if (c.criticalNotification) return false;
    if (c.onScreenDisplay) return false;
    if (c.appletPopup) return false;
    if (c.transient) return false;
    // Most normal windows have normalWindow=true, but keep fallback.
    if (c.normalWindow === false && c.dialog === true) return false;
    return true;
  }

  function clientTitle(c) {
    try { return "" + (c.caption || ""); } catch (e) { return ""; }
  }

  function clientClassLike(c) {
    // Best-effort; fields differ between X11/XWayland/Wayland.
    try {
      if (!c) return "";
      if (c.resourceClass) return "" + c.resourceClass;
      if (c.resourceName) return "" + c.resourceName;
      if (c.windowClass) return "" + c.windowClass;
      if (c.appId) return "" + c.appId;
      if (c.desktopFileName) return "" + c.desktopFileName;
    } catch (e) {}
    return "";
  }

  function isFullscreenClient(c) {
    try {
      if (!c) return false;
      if (c.fullScreen === true) return true;
      if (c.fullscreen === true) return true;
    } catch (e) {}
    return false;
  }

  function isParentedLikeClient(c) {
    try {
      if (!c) return false;
      // Dialog-like windows should not be auto-captured.
      if (c.transient === true) return true;
      if (c.dialog === true) return true;
      if (c.modal === true) return true;
      // Some KWin builds expose the parent on this property.
      if (c.transientFor) return true;
    } catch (e) {
      warnOnce("isParentedLikeClient", "Failed to check whether window is dialog/transient", e);
    }
    return false;
  }

  function matchText(val, pattern, mode) {
    var v = (val || "");
    var p = (pattern || "");
    if (p.length === 0) return true;
    if (v.length === 0) return false;
    var m = (mode || "Anywhere");
    if (m === "Exact") return v === p;
    if (m === "Prefix") return v.indexOf(p) === 0;
    if (m === "Suffix") return v.lastIndexOf(p) === (v.length - p.length);
    // Anywhere default
    return v.indexOf(p) >= 0;
  }

  function matchFilterClause(val, clause) {
    if (!clause) return true;
    var value = "";
    var mode = "Anywhere";
    try {
      if (typeof clause === "string") { value = clause; mode = "Anywhere"; }
      else {
        value = "" + (clause.value || clause.Value || "");
        mode = "" + (clause.match || clause.Match || "Anywhere");
      }
    } catch (e) {
      return true;
    }
    if (!value) return true;
    return matchText(val, value, mode);
  }

  function matchesAnyIgnoreFilter(c, filters) {
    if (!filters || !filters.length) return false;
    var title = clientTitle(c);
    var cls = clientClassLike(c);
    for (var i = 0; i < filters.length; i++) {
      var f = filters[i];
      if (!f) continue;
      var ok = true;
      // If a clause exists, it must match. Empty clauses are treated as wildcards.
      if (!matchFilterClause(title, f.title || f.Title)) ok = false;
      if (!matchFilterClause(cls, f.class || f.Class)) ok = false;
      // app/process matching is best-effort: compare against class-like as we can't read /proc from KWin scripts.
      if (!matchFilterClause(cls, f.app || f.process || f.Process)) ok = false;
      if (ok) return true;
    }
    return false;
  }

  function clientKey(c) {
    // Wayland: internalId exists; X11: windowId exists.
    if (c.internalId) return "" + c.internalId;
    if (c.windowId) return "" + c.windowId;
    // fallback: caption + geometry (unstable)
    return "" + c.caption + "@" + safeStringify(rectToObj(c.frameGeometry));
  }

  function getClientScreen(c) {
    if (typeof c.screen === "number") return c.screen;
    if (typeof c.output === "number") return c.output;
    return 0;
  }

  function getCurrentDesktop() {
    try {
      return workspace.currentDesktop;
    } catch (e) {
      warnOnce("getCurrentDesktop", "workspace.currentDesktop failed", e);
      return 1;
    }
  }

  function clientAreaType() {
    // Prefer areas that respect panels/struts (avoid covering taskbar).
    if (typeof KWin !== "undefined" && KWin.MaximizeArea !== undefined) return KWin.MaximizeArea;
    if (typeof KWin !== "undefined" && KWin.WorkArea !== undefined) return KWin.WorkArea;
    if (typeof KWin !== "undefined" && KWin.PlacementArea !== undefined) return KWin.PlacementArea;
    if (typeof KWin !== "undefined" && KWin.FullScreenArea !== undefined) return KWin.FullScreenArea;
    if (typeof KWin !== "undefined" && KWin.FullArea !== undefined) return KWin.FullArea;
    return 0;
  }

  function fullAreaType() {
    if (typeof KWin !== "undefined" && KWin.FullArea !== undefined) return KWin.FullArea;
    if (typeof KWin !== "undefined" && KWin.FullScreenArea !== undefined) return KWin.FullScreenArea;
    return clientAreaType();
  }

  function screenAtPoint(p) {
    try {
      if (workspace && isFn(workspace.screenAt)) return workspace.screenAt(p);
    } catch (e0) { warnOnce("screenAtPoint.screenAt", "workspace.screenAt failed", e0); }
    try {
      if (workspace && isFn(workspace.screenAtPosition)) return workspace.screenAtPosition(p);
    } catch (e1) { warnOnce("screenAtPoint.screenAtPosition", "workspace.screenAtPosition failed", e1); }
    return null;
  }

  function clientAreaForWindow(c) {
    if (!workspace || !isFn(workspace.clientArea) || !c) return rect(0, 0, 1920, 1080);
    try {
      // Try a few area types in preference order (some builds may throw for some types).
      var types = [];
      if (typeof KWin !== "undefined") {
        if (KWin.MaximizeArea !== undefined) types.push(KWin.MaximizeArea);
        if (KWin.WorkArea !== undefined) types.push(KWin.WorkArea);
        if (KWin.PlacementArea !== undefined) types.push(KWin.PlacementArea);
        if (KWin.FullScreenArea !== undefined) types.push(KWin.FullScreenArea);
        if (KWin.FullArea !== undefined) types.push(KWin.FullArea);
      }
      if (types.length === 0) types.push(clientAreaType());

      var desktop = getCurrentDesktop();
      var screenIdx = getClientScreen(c);

      var full = null;
      try { if (typeof KWin !== "undefined" && KWin.FullArea !== undefined) full = workspace.clientArea(KWin.FullArea, screenIdx, desktop); } catch (eF0) { warnOnce("clientAreaForWindow.full.screen", "clientArea FullArea(screen,desktop) failed", eF0); }
      try { if (!full) full = workspace.clientArea(fullAreaType(), screenIdx, desktop); } catch (eF1) { warnOnce("clientAreaForWindow.full.fallback", "clientArea fullAreaType(screen,desktop) failed", eF1); }
      try { if (!full) full = workspace.clientArea(fullAreaType(), c); } catch (eF2) { warnOnce("clientAreaForWindow.full.client", "clientArea fullAreaType(client) failed", eF2); }

      for (var i = 0; i < types.length; i++) {
        try {
          // Try both overloads and pick the smaller (more strut-aware) area when they disagree.
          var aClient = null;
          var aScreen = null;
          try { aClient = workspace.clientArea(types[i], c); } catch (eS0) { warnOnce("clientAreaForWindow.type." + types[i] + ".client", "clientArea(type, client) failed", eS0); aClient = null; }
          try { aScreen = workspace.clientArea(types[i], screenIdx, desktop); } catch (eS1) { warnOnce("clientAreaForWindow.type." + types[i] + ".screen", "clientArea(type, screen, desktop) failed", eS1); aScreen = null; }
          var a = pickSmallerArea(aClient, aScreen);
          if (a && a.width > 0 && a.height > 0) return full ? rectIntersect(a, full) : a;
        } catch (e1) {
          warnOnce("clientAreaForWindow.type." + types[i], "clientArea probing failed", e1);
        }
      }

      var fallback = null;
      try { fallback = workspace.clientArea(clientAreaType(), screenIdx, desktop); } catch (e2) { warnOnce("clientAreaForWindow.fallback.screen", "clientArea(clientAreaType, screen, desktop) failed", e2); }
      if (!fallback) {
        try { fallback = workspace.clientArea(clientAreaType(), c); } catch (e3) { warnOnce("clientAreaForWindow.fallback.client", "clientArea(clientAreaType, client) failed", e3); }
      }
      return full ? rectIntersect(fallback, full) : fallback;
    } catch (e0) {
      warnOnce("clientAreaForWindow", "clientAreaForWindow failed", e0);
      return rect(0, 0, 1920, 1080);
    }
  }

  function clientAreaForPoint(p) {
    if (!workspace || !isFn(workspace.clientArea) || !p) return null;
    var t = clientAreaType();
    if (typeof KWin !== "undefined" && KWin.MaximizeArea !== undefined) t = KWin.MaximizeArea;
    else if (typeof KWin !== "undefined" && KWin.WorkArea !== undefined) t = KWin.WorkArea;
    else if (typeof KWin !== "undefined" && KWin.PlacementArea !== undefined) t = KWin.PlacementArea;
    var desktop = getCurrentDesktop();
    var screenIdx = screenAtPoint(p);
    var full = null;
    if (screenIdx !== null && screenIdx !== undefined) {
      try { if (typeof KWin !== "undefined" && KWin.FullArea !== undefined) full = workspace.clientArea(KWin.FullArea, screenIdx, desktop); } catch (eF0) { warnOnce("clientAreaForPoint.full.screen", "clientArea FullArea(screen,desktop) failed", eF0); }
      try { if (!full) full = workspace.clientArea(fullAreaType(), screenIdx, desktop); } catch (eF1) { warnOnce("clientAreaForPoint.full.fallback", "clientArea fullAreaType(screen,desktop) failed", eF1); }
    }

    // Prefer point overloads, but compare with screen overload and pick the smaller (more strut-aware) area.
    var aPoint = null;
    try { aPoint = workspace.clientArea(t, p, desktop); } catch (e0) { warnOnce("clientAreaForPoint.type.point.desktop", "clientArea(type, point, desktop) failed", e0); aPoint = null; }
    if (!aPoint) { try { aPoint = workspace.clientArea(t, p); } catch (e1) { warnOnce("clientAreaForPoint.type.point", "clientArea(type, point) failed", e1); aPoint = null; } }

    var aScreen = null;
    if (screenIdx !== null && screenIdx !== undefined) {
      try { aScreen = workspace.clientArea(t, screenIdx, desktop); } catch (eS0) { warnOnce("clientAreaForPoint.type.screen", "clientArea(type, screen, desktop) failed", eS0); aScreen = null; }
    }

    var a = pickSmallerArea(aPoint, aScreen);
    if (a && a.width > 0 && a.height > 0) return full ? rectIntersect(a, full) : a;

    try { return workspace.clientArea(clientAreaType(), p, desktop); } catch (e2) { warnOnce("clientAreaForPoint.fallback.point.desktop", "clientArea(clientAreaType, point, desktop) failed", e2); }
    try { return workspace.clientArea(clientAreaType(), p); } catch (e3) { warnOnce("clientAreaForPoint.fallback.point", "clientArea(clientAreaType, point) failed", e3); }
    return null;
  }

  function normalizeLayout(layout) {
    if (!layout) return null;
    if (!layout.id) return null;
    if (!layout.zones) layout.zones = [];
    if (!layout.tabGroups) layout.tabGroups = [];
    // Build lookup
    var zoneById = {};
    for (var i = 0; i < layout.zones.length; i++) {
      var z = layout.zones[i];
      if (z && z.id) zoneById[z.id] = z;
    }
    layout._zoneById = zoneById;
    return layout;
  }

  function isDisabledLayoutId(layoutId) {
    return ("" + (layoutId || "")).trim().toLowerCase() === "disabled";
  }

  function StackState() {
    // KWin scripts do not expose QFile/QDir in this environment, so we keep state in memory.
    // Configuration can be provided via KWin script config (readConfig).
    this.state = {
      layoutId: null,
      assignments: {}, // clientKey -> { zoneId, restore: {x,y,w,h}, screen }
      zoneStacks: {}, // zoneId -> [clientKey] (stable ordering)
    };
    this.config = {
      layoutId: "",
      defaultLayoutId: "horizontal-info",
      screenLayoutMap: {}, // optional: "WxH@X,Y" -> layoutId (JSON in ScreenLayoutMap)
      screenKeys: [], // optional: ["WxH@X,Y", ...] (JSON in ScreenKeys)
      captureOnStart: false,
      captureOnConfigChange: false,
      // Extra top space (in px) reserved for a custom header/tab bar (primarily for the customized layout).
      mainHeaderPx: 0,
      autoCaptureOnNewWindow: false,
      autoCaptureDefaultZoneId: "main",
      // Default ignore filters for auto-capture. Users can override via AutoCaptureIgnoreFilters JSON.
      autoCaptureIgnoreFilters: [{ title: "YR Client" }, { title: "Dota 2" }],
    };
  }

  StackState.prototype.load = function () {
    try {
      if (typeof readConfig === "function") {
        this.config.layoutId = "" + readConfig("LayoutId", "");
        this.config.defaultLayoutId = "" + readConfig("DefaultLayoutId", this.config.defaultLayoutId);
        this.config.captureOnStart = ("" + readConfig("CaptureOnStart", "false")) === "true";
        this.config.captureOnConfigChange = ("" + readConfig("CaptureOnConfigChange", "false")) === "true";
        try {
          var headerStr = "" + readConfig("MainHeaderPx", "0");
          var headerPx = parseInt(headerStr, 10);
          if (isNaN(headerPx) || headerPx < 0) headerPx = 0;
          this.config.mainHeaderPx = headerPx;
        } catch (eH) {
          warnOnce("StackState.load.MainHeaderPx", "Failed to parse MainHeaderPx", eH);
        }
        var mapStr = "" + readConfig("ScreenLayoutMap", "{}");
        try {
          var parsed = JSON.parse(mapStr);
          if (parsed) this.config.screenLayoutMap = parsed;
        } catch (e2) {
          warnOnce("StackState.load.ScreenLayoutMap", "Bad JSON in ScreenLayoutMap: " + truncate(mapStr, 200), e2);
        }

        var keysStr = "" + readConfig("ScreenKeys", "[]");
        try {
          var keysParsed = JSON.parse(keysStr);
          if (keysParsed && keysParsed.length) this.config.screenKeys = keysParsed;
        } catch (e3) {
          warnOnce("StackState.load.ScreenKeys", "Bad JSON in ScreenKeys: " + truncate(keysStr, 200), e3);
        }

        this.config.autoCaptureOnNewWindow = ("" + readConfig("AutoCaptureOnNewWindow", "false")) === "true";
        this.config.autoCaptureDefaultZoneId = "" + readConfig("AutoCaptureDefaultZoneId", this.config.autoCaptureDefaultZoneId);
        var ignoresStr = "" + readConfig("AutoCaptureIgnoreFilters", JSON.stringify(this.config.autoCaptureIgnoreFilters || []));
        try {
          var ignoresParsed = JSON.parse(ignoresStr);
          if (ignoresParsed && Array.isArray(ignoresParsed)) this.config.autoCaptureIgnoreFilters = ignoresParsed;
        } catch (e4) {
          warnOnce("StackState.load.AutoCaptureIgnoreFilters", "Bad JSON in AutoCaptureIgnoreFilters: " + truncate(ignoresStr, 200), e4);
        }
      }
    } catch (e) {
      warnOnce("StackState.load", "Failed to load config via readConfig", e);
    }
  };

  StackState.prototype.save = function () {
    // no-op (no persistence here)
  };

  function LayoutEngine(state) {
    this.state = state;

    this.layouts = {}; // id -> layout
    this.layout = null;
    this.layoutId = null;
    this._layoutsLoaded = false;

    this.zoneWindows = {}; // zoneId -> [clientKey]
    this.clientsByKey = {}; // clientKey -> client

    this._moveTimer = null;
    this._moveClient = null;
    this._moveCandidateZoneId = null;
    this._moveCandidateAreaKey = null;

    this._debugOutlineOn = false;
  }

  LayoutEngine.prototype._layoutsDirPaths = function () {
    return [];
  };

  LayoutEngine.prototype._loadBundledLayouts = function () {
    // no-op (no file IO)
  };

  LayoutEngine.prototype._fallbackLayouts = function () {
    // Minimal in-code fallback if file loading fails.
	    var horizontalInfo = normalizeLayout({
	      id: "horizontal-info",
	      name: "Horizontal + Info (fallback)",
	      zones: [
	        { id: "info", mode: "stack", rect: { x: 0.0, y: 0.0, w: 0.14, h: 1.0 } },
	        { id: "main", mode: "stack", rect: { x: 0.14, y: 0.0, w: 0.62, h: 1.0 } },
	        { id: "rightSingle", mode: "stack", rect: { x: 0.76, y: 0.0, w: 0.24, h: 1.0 } },
	        { id: "rightStack", mode: "tileVertical", rect: { x: 0.76, y: 0.0, w: 0.24, h: 1.0 } },
	        { id: "rightStackDrop", isDropZone: true, targetZoneId: "rightStack", rect: { x: 0.76, y: 0.0, w: 0.24, h: 1.0 } },
	        { id: "rightSingleDrop", isDropZone: true, targetZoneId: "rightSingle", rect: { x: 0.83, y: 0.42, w: 0.10, h: 0.16 } }
	      ],
	      tabGroups: [
	        { id: "main", name: "Main", zones: ["main"] },
	        { id: "right", name: "Right", zones: ["rightSingle", "rightStack"] }
	      ]
	    });

	    var largeLeft = normalizeLayout({
	      id: "large-horizontal-left",
	      name: "Large Horizontal Left (fallback)",
	      zones: [
	        { id: "sideSingle", name: "Side Single", mode: "stack", rect: { x: 0.00, y: 0.00, w: 0.22, h: 1.00 } },
	        { id: "sideStack", name: "Side Stack", mode: "tileVertical", rect: { x: 0.00, y: 0.00, w: 0.22, h: 1.00 } },

	        { id: "main", name: "Main", mode: "stack", rect: { x: 0.22, y: 0.00, w: 0.56, h: 1.00 } },
	        { id: "leftMain", name: "Left Main", mode: "stack", rect: { x: 0.22, y: 0.00, w: 0.28, h: 1.00 } },
	        { id: "rightMain", name: "Right Main", mode: "stack", rect: { x: 0.50, y: 0.00, w: 0.28, h: 1.00 } },
	        { id: "topMain", name: "Top Main", mode: "stack", rect: { x: 0.22, y: 0.00, w: 0.56, h: 0.50 } },
	        { id: "bottomMain", name: "Bottom Main", mode: "stack", rect: { x: 0.22, y: 0.50, w: 0.56, h: 0.50 } },

	        { id: "topLeft", name: "Top Left", mode: "stack", rect: { x: 0.22, y: 0.00, w: 0.39, h: 0.50 } },
	        { id: "topRight", name: "Top Right", mode: "stack", rect: { x: 0.61, y: 0.00, w: 0.17, h: 0.50 } },
	        { id: "bottomLeft", name: "Bottom Left", mode: "stack", rect: { x: 0.22, y: 0.50, w: 0.39, h: 0.50 } },
	        { id: "bottomRight", name: "Bottom Right", mode: "stack", rect: { x: 0.61, y: 0.50, w: 0.17, h: 0.50 } },

	        { id: "rightSingle", name: "Right Single", mode: "stack", rect: { x: 0.78, y: 0.00, w: 0.22, h: 1.00 } },
	        { id: "rightStack", name: "Right Stack", mode: "tileVertical", rect: { x: 0.78, y: 0.00, w: 0.22, h: 1.00 } },

	        { id: "sideDropStack", name: "Side Drop (Stack)", isDropZone: true, targetZoneId: "sideStack", rect: { x: 0.00, y: 0.00, w: 0.22, h: 1.00 } },
	        { id: "sideDropSingle", name: "Side Drop (Single)", isDropZone: true, targetZoneId: "sideSingle", rect: { x: 0.06, y: 0.42, w: 0.10, h: 0.16 } },

	        { id: "rightDropStack", name: "Right Drop (Stack)", isDropZone: true, targetZoneId: "rightStack", rect: { x: 0.78, y: 0.00, w: 0.22, h: 1.00 } },
	        { id: "rightDropSingle", name: "Right Drop (Single)", isDropZone: true, targetZoneId: "rightSingle", rect: { x: 0.84, y: 0.42, w: 0.10, h: 0.16 } }
	      ],
	      tabGroups: [
	        { id: "main", name: "Main", zones: ["main", "leftMain", "rightMain", "topMain", "bottomMain", "topLeft", "topRight", "bottomLeft", "bottomRight"] },
	        { id: "side", name: "Side", zones: ["sideStack", "sideSingle"] },
	        { id: "right", name: "Right", zones: ["rightStack", "rightSingle"] }
	      ]
	    });

	    var largeRight = normalizeLayout({
	      id: "large-horizontal-right",
	      name: "Large Horizontal Right (fallback)",
	      zones: [
	        { id: "main", name: "Main", mode: "stack", rect: { x: 0.00, y: 0.00, w: 0.75, h: 1.00 } },
	        { id: "leftMain", name: "Left Main", mode: "stack", rect: { x: 0.00, y: 0.00, w: 0.375, h: 1.00 } },
	        { id: "rightMain", name: "Right Main", mode: "stack", rect: { x: 0.375, y: 0.00, w: 0.375, h: 1.00 } },
	        { id: "topMain", name: "Top Main", mode: "stack", rect: { x: 0.00, y: 0.00, w: 0.75, h: 0.50 } },
	        { id: "bottomMain", name: "Bottom Main", mode: "stack", rect: { x: 0.00, y: 0.50, w: 0.75, h: 0.50 } },

	        { id: "topLeft", name: "Top Left", mode: "stack", rect: { x: 0.00, y: 0.00, w: 0.375, h: 0.50 } },
	        { id: "topRight", name: "Top Right", mode: "stack", rect: { x: 0.375, y: 0.00, w: 0.375, h: 0.50 } },
	        { id: "bottomLeft", name: "Bottom Left", mode: "stack", rect: { x: 0.00, y: 0.50, w: 0.375, h: 0.50 } },
	        { id: "bottomRight", name: "Bottom Right", mode: "stack", rect: { x: 0.375, y: 0.50, w: 0.375, h: 0.50 } },

	        { id: "sideSingle", name: "Side Single", mode: "stack", rect: { x: 0.75, y: 0.00, w: 0.25, h: 1.00 } },
	        { id: "sideStack", name: "Side Stack", mode: "tileVertical", rect: { x: 0.75, y: 0.00, w: 0.25, h: 1.00 } },
	        { id: "sideDropStack", name: "Side Drop (Stack)", isDropZone: true, targetZoneId: "sideStack", rect: { x: 0.75, y: 0.00, w: 0.25, h: 1.00 } },
	        { id: "sideDropSingle", name: "Side Drop (Single)", isDropZone: true, targetZoneId: "sideSingle", rect: { x: 0.825, y: 0.42, w: 0.10, h: 0.16 } }
	      ],
	      tabGroups: [
	        { id: "side", name: "Side", zones: ["sideStack", "sideSingle"] },
	        { id: "main", name: "Main", zones: ["main", "leftMain", "rightMain", "topMain", "bottomMain", "topLeft", "topRight", "bottomLeft", "bottomRight"] }
	      ]
	    });

	    var oob = normalizeLayout({
      id: "oob-horizontal",
      name: "OOB Horizontal (fallback)",
      zones: [
        { id: "main", mode: "stack", rect: { x: 0.2, y: 0.0, w: 0.6, h: 1.0 } },
        { id: "leftSingle", mode: "stack", rect: { x: 0.0, y: 0.0, w: 0.2, h: 1.0 } },
        { id: "leftStack", mode: "tileVertical", rect: { x: 0.0, y: 0.0, w: 0.2, h: 1.0 } },
        { id: "rightSingle", mode: "stack", rect: { x: 0.8, y: 0.0, w: 0.2, h: 1.0 } },
        { id: "rightStack", mode: "tileVertical", rect: { x: 0.8, y: 0.0, w: 0.2, h: 1.0 } },
        { id: "leftDropStack", isDropZone: true, targetZoneId: "leftStack", rect: { x: 0.0, y: 0.0, w: 0.2, h: 1.0 } },
        { id: "leftDropSingle", isDropZone: true, targetZoneId: "leftSingle", rect: { x: 0.05, y: 0.42, w: 0.1, h: 0.16 } },
        { id: "rightDropStack", isDropZone: true, targetZoneId: "rightStack", rect: { x: 0.8, y: 0.0, w: 0.2, h: 1.0 } },
        { id: "rightDropSingle", isDropZone: true, targetZoneId: "rightSingle", rect: { x: 0.85, y: 0.42, w: 0.1, h: 0.16 } }
      ],
      tabGroups: [
        { id: "main", name: "Main", zones: ["main"] },
        { id: "left", name: "Left", zones: ["leftSingle", "leftStack"] },
        { id: "right", name: "Right", zones: ["rightSingle", "rightStack"] }
      ]
    });

	    if (!this.layouts[horizontalInfo.id]) this.layouts[horizontalInfo.id] = horizontalInfo;
	    if (!this.layouts[largeLeft.id]) this.layouts[largeLeft.id] = largeLeft;
	    if (!this.layouts[largeRight.id]) this.layouts[largeRight.id] = largeRight;
	    if (!this.layouts[oob.id]) this.layouts[oob.id] = oob;

      // Port (approx) of Windows "Large Horizontal Left - Customized.xaml"
      var largeLeftCustomized = normalizeLayout({
        id: "large-horizontal-left-customized",
        name: "Large Horizontal Left (Customized, approx)",
        zones: (function () {
          var sideW = 1.2 / 4.2; // from XAML ColumnDefinitions: 1.2* + 3*
          var mainX = sideW;
          var mainW = 1.0 - sideW;
          var leftW = mainW * (1.2 / 2.2); // from inner grid: 1.2* + 1*
          var rightW = mainW * (1.0 / 2.2);
          var centerGripW = Math.min(0.08 * mainW, 0.06); // approx of 80px on 1024 design width
          var centerGripX = mainX + mainW / 2 - centerGripW / 2;
          var mainDropW = Math.min(0.14 * mainW, 0.12);
          var mainDropH = Math.min(0.20, 0.18);

          return [
            // Left column (side)
            // Leave space at the top/bottom for widgets/panes on the main screen.
            { id: "sideSingle", name: "Side Single", mode: "stack", rect: { x: 0.00, y: 0.00, w: sideW, h: 1.00 }, insetsPx: { t: 200, b: 440 } },
            { id: "sideStack", name: "Side Stack", mode: "tileVertical", rect: { x: 0.00, y: 0.00, w: sideW, h: 1.00 }, insetsPx: { t: 200, b: 440 } },
            { id: "sideDropStack", name: "Side Drop (Stack)", isDropZone: true, targetZoneId: "sideStack", rect: { x: 0.00, y: 0.00, w: sideW, h: 1.00 }, insetsPx: { t: 200, b: 440 } },
            // centered-ish drop to side single
            { id: "sideDropSingle", name: "Side Drop (Single)", isDropZone: true, targetZoneId: "sideSingle", rect: { x: sideW * 0.5 - 0.06, y: 0.42, w: 0.12, h: 0.16 } },

            // Right column (main)
            // NOTE: main column has a fixed top offset to make room for a manually placed tab bar widget.
            { id: "main", name: "Main", mode: "stack", rect: { x: mainX, y: 0.00, w: mainW, h: 1.00 }, insetsPx: { t: 60 } },
            { id: "leftMain", name: "Left Main", mode: "stack", rect: { x: mainX, y: 0.00, w: leftW, h: 1.00 }, insetsPx: { t: 60, b: 100 } },
            { id: "rightMain", name: "Right Main", mode: "stack", rect: { x: mainX + leftW, y: 0.00, w: rightW, h: 1.00 }, insetsPx: { t: 60, b: 100 } },

            { id: "topMain", name: "Top Main", mode: "stack", rect: { x: mainX, y: 0.00, w: mainW, h: 0.50 }, insetsPx: { t: 60 } },
            { id: "bottomMain", name: "Bottom Main", mode: "stack", rect: { x: mainX, y: 0.50, w: mainW, h: 0.50 }, insetsPx: { t: 60 } },

            { id: "topLeft", name: "Top Left", mode: "stack", rect: { x: mainX, y: 0.00, w: leftW, h: 0.50 }, insetsPx: { t: 60 } },
            { id: "topRight", name: "Top Right", mode: "stack", rect: { x: mainX + leftW, y: 0.00, w: rightW, h: 0.50 }, insetsPx: { t: 60 } },
            { id: "bottomLeft", name: "Bottom Left", mode: "stack", rect: { x: mainX, y: 0.50, w: leftW, h: 0.50 }, insetsPx: { t: 60 } },
            { id: "bottomRight", name: "Bottom Right", mode: "stack", rect: { x: mainX + leftW, y: 0.50, w: rightW, h: 0.50 }, insetsPx: { t: 60 } },

            // Drop zones (targets)
            { id: "leftDropMain", name: "Left Main Drop", isDropZone: true, targetZoneId: "leftMain", rect: { x: mainX, y: 0.00, w: leftW, h: 1.00 }, insetsPx: { t: 60 } },
            { id: "rightDropMain", name: "Right Main Drop", isDropZone: true, targetZoneId: "rightMain", rect: { x: mainX + leftW, y: 0.00, w: rightW, h: 1.00 }, insetsPx: { t: 60 } },

            { id: "topDropMain", name: "Top Main Drop", isDropZone: true, targetZoneId: "topMain", rect: { x: centerGripX, y: 0.00, w: centerGripW, h: 0.50 }, insetsPx: { t: 60 } },
            { id: "bottomDropMain", name: "Bottom Main Drop", isDropZone: true, targetZoneId: "bottomMain", rect: { x: centerGripX, y: 0.50, w: centerGripW, h: 0.50 }, insetsPx: { t: 60 } },

            { id: "mainDrop", name: "Main Drop", isDropZone: true, targetZoneId: "main", rect: { x: mainX + mainW / 2 - mainDropW / 2, y: 0.5 - mainDropH / 2, w: mainDropW, h: mainDropH }, insetsPx: { t: 60 } }
          ];
        })(),
        tabGroups: [
          { id: "side", name: "Side", zones: ["sideStack", "sideSingle"] },
          { id: "main", name: "Main (all)", zones: ["main", "leftMain", "rightMain", "topMain", "bottomMain", "topLeft", "topRight", "bottomLeft", "bottomRight"] }
        ]
      });
      if (!this.layouts[largeLeftCustomized.id]) this.layouts[largeLeftCustomized.id] = largeLeftCustomized;
	  };

  LayoutEngine.prototype.loadLayouts = function () {
    this._loadBundledLayouts();
    this._fallbackLayouts();
  };

  LayoutEngine.prototype._ensureLayoutsLoaded = function () {
    if (this._layoutsLoaded) return;
    this.loadLayouts();
    this._layoutsLoaded = true;
  };

  LayoutEngine.prototype._layoutKeyForArea = function (area) {
    return "" + area.width + "x" + area.height + "@" + area.x + "," + area.y;
  };

  LayoutEngine.prototype._parseAreaKey = function (key) {
    // "WxH@X,Y"
    if (!key) return null;
    var s = "" + key;
    var at = s.indexOf("@");
    if (at < 0) return null;
    var wh = s.substring(0, at);
    var xy = s.substring(at + 1);
    var xIdx = wh.indexOf("x");
    var comma = xy.indexOf(",");
    if (xIdx < 0 || comma < 0) return null;
    var w = parseInt(wh.substring(0, xIdx), 10);
    var h = parseInt(wh.substring(xIdx + 1), 10);
    var x = parseInt(xy.substring(0, comma), 10);
    var y = parseInt(xy.substring(comma + 1), 10);
    if (isNaN(w) || isNaN(h) || isNaN(x) || isNaN(y)) return null;
    return { width: w, height: h, x: x, y: y };
  };

  LayoutEngine.prototype._referenceClient = function () {
    try {
      var a = workspace.activeWindow || workspace.activeClient;
      if (a && isManageableClient(a)) return a;
    } catch (e0) { warnOnce("referenceClient.active", "Failed to get active window/client", e0); }
    try {
      var clients = null;
      if (workspace.windowList) clients = workspace.windowList();
      else if (workspace.clientList) clients = workspace.clientList();
      if (clients && clients.length > 0) return clients[0];
    } catch (e1) { warnOnce("referenceClient.list", "Failed to list windows/clients", e1); }
    return null;
  };

  LayoutEngine.prototype._screensForTraversal = function () {
    var keys = [];
    if (this.state && this.state.config && this.state.config.screenKeys && this.state.config.screenKeys.length) {
      keys = this.state.config.screenKeys;
    } else if (this.state && this.state.config && this.state.config.screenLayoutMap) {
      for (var k in this.state.config.screenLayoutMap) keys.push(k);
    }

    var rects = [];
    var seen = {};
    for (var i = 0; i < keys.length; i++) {
      var key = keys[i];
      if (!key || seen[key]) continue;
      seen[key] = true;
      var r = this._parseAreaKey(key);
      if (!r) continue;
      rects.push({ key: key, rect: r });
    }
    return rects;
  };

  LayoutEngine.prototype._matchScreenKeyForArea = function (area) {
    var screens = this._screensForTraversal();
    if (!area || screens.length === 0) return this._layoutKeyForArea(area);

    // Pick screen whose rect overlaps the area best.
    var bestKey = null;
    var bestScore = null;
    for (var i = 0; i < screens.length; i++) {
      var sr = screens[i].rect;
      var key = screens[i].key;

      // Score by how close area origin is to screen rect, plus width match preference.
      var dx = Math.abs((sr.x || 0) - (area.x || 0));
      var dy = Math.abs((sr.y || 0) - (area.y || 0));
      var dw = Math.abs((sr.width || 0) - (area.width || 0));
      var score = dx * 10 + dy * 10 + dw;
      if (bestScore === null || score < bestScore) { bestScore = score; bestKey = key; }
    }
    return bestKey || this._layoutKeyForArea(area);
  };

  LayoutEngine.prototype.pickLayoutIdForCurrentContext = function () {
    var ref = this._referenceClient();
    if (ref) {
      var area = clientAreaForWindow(ref);
      // Use the same logic as per-window selection (includes fuzzy ScreenLayoutMap match).
      return this._pickLayoutIdForArea(area);
    }
    return this.state.config.defaultLayoutId || "horizontal-info";
  };

  LayoutEngine.prototype.setLayout = function (layoutId) {
    if (!layoutId || !this.layouts[layoutId]) return false;
    this.layoutId = layoutId;
    this.layout = this.layouts[layoutId];
    this.state.state.layoutId = layoutId;
    this.state.save();
    log("Layout set: " + layoutId);
    this.reflowAll();
    return true;
  };

  LayoutEngine.prototype.ensureLayout = function () {
    if (this.layout) return;
    this._ensureLayoutsLoaded();
    var desired = this.state.config.layoutId || this.state.state.layoutId;
    if (!desired) desired = this.pickLayoutIdForCurrentContext();
    if (!this.layouts[desired]) desired = this.state.config.defaultLayoutId || "horizontal-info";
    if (!this.layouts[desired]) {
      // fall back to any layout.
      for (var id in this.layouts) {
        desired = id;
        break;
      }
    }
    this.setLayout(desired);
  };

  LayoutEngine.prototype._pickLayoutIdForArea = function (area, screenKey) {
    if (!area) return this.state.config.defaultLayoutId || "horizontal-info";
    // If LayoutId is set in config, treat it as a global override.
    if (this.state.config && this.state.config.layoutId) {
      var pinned = ("" + this.state.config.layoutId).trim();
      if (pinned.length > 0) return pinned;
    }

    var key = this._layoutKeyForArea(area);
    if (this.state.config && this.state.config.screenLayoutMap) {
      var map = this.state.config.screenLayoutMap;
      if (screenKey && map[screenKey]) return map[screenKey];
      if (map[key]) return map[key];

      // Best-effort fuzzy match to tolerate panel offsets / slightly different usable areas:
      // pick the closest mapped rect in terms of origin+size.
      var best = null;
      var bestScore = null;
      for (var k in map) {
        if (!map[k]) continue;
        var p = this._parseAreaKey(k);
        if (!p) continue;
        var dx = Math.abs(p.x - area.x);
        var dy = Math.abs(p.y - area.y);
        var dw = Math.abs(p.width - area.width);
        var dh = Math.abs(p.height - area.height);
        // Penalize origin mismatch more than size mismatch.
        var score = dx * 8 + dy * 8 + dw * 2 + dh * 2;
        if (bestScore === null || score < bestScore) {
          bestScore = score;
          best = map[k];
        }
      }
      if (best) return best;
    }

    if (area.width >= 3200) return area.x >= 0 ? "large-horizontal-right" : "large-horizontal-left";
    return this.state.config.defaultLayoutId || "horizontal-info";
  };

  LayoutEngine.prototype._ctxForClient = function (c) {
    this._ensureLayoutsLoaded();
    var area = clientAreaForWindow(c);
    var full = null;
    try {
      if (workspace && isFn(workspace.clientArea)) {
        var screenIdx = getClientScreen(c);
        var desktop = getCurrentDesktop();
        try { if (typeof KWin !== "undefined" && KWin.FullArea !== undefined) full = workspace.clientArea(KWin.FullArea, screenIdx, desktop); } catch (e0) { warnOnce("ctxForClient.full.screen", "clientArea FullArea(screen,desktop) failed", e0); }
        try { if (!full) full = workspace.clientArea(fullAreaType(), screenIdx, desktop); } catch (e1) { warnOnce("ctxForClient.full.fallback", "clientArea fullAreaType(screen,desktop) failed", e1); }
      }
    } catch (e2) { warnOnce("ctxForClient.full", "Failed to compute full area", e2); }
    var areaKey = this._layoutKeyForArea(area);
    var screenKey = this._matchScreenKeyForArea(full || area);

    if (this._areaLayoutOverride && this._areaLayoutOverride[areaKey]) {
      var ov = this._areaLayoutOverride[areaKey];
      if (ov && this.layouts[ov]) {
        var ovLayout = this.layouts[ov];
        this.layout = ovLayout;
        this.layoutId = ov;
        return { area: area, fullArea: full, areaKey: areaKey, screenKey: screenKey, layoutId: ov, layout: ovLayout };
      }
    }

    var layoutId = this._pickLayoutIdForArea(area, screenKey);
    var layout = isDisabledLayoutId(layoutId) ? null : (this.layouts[layoutId] || this.layouts[this.state.config.defaultLayoutId] || null);
    if (!layout && !isDisabledLayoutId(layoutId)) {
      for (var id in this.layouts) { layout = this.layouts[id]; layoutId = id; break; }
    }
    // keep a "current" layout for legacy callers (listZones/listTabGroups)
    if (layout) { this.layout = layout; this.layoutId = layoutId; }
    return { area: area, fullArea: full, areaKey: areaKey, screenKey: screenKey, layoutId: layoutId, layout: layout };
  };

  LayoutEngine.prototype._matchScreenKeyForPoint = function (p) {
    var screens = this._screensForTraversal();
    if (!p || !screens || screens.length === 0) return null;

    // Prefer a screen that contains the point.
    for (var i = 0; i < screens.length; i++) {
      var sr = screens[i].rect;
      if (sr && pointInRect(p, sr)) return screens[i].key;
    }

    // Otherwise pick the nearest rect by distance to bounds.
    var bestKey = null;
    var bestScore = null;
    for (var j = 0; j < screens.length; j++) {
      var r = screens[j].rect;
      if (!r) continue;
      var dx = 0;
      if (p.x < r.x) dx = r.x - p.x;
      else if (p.x > r.x + r.width) dx = p.x - (r.x + r.width);
      var dy = 0;
      if (p.y < r.y) dy = r.y - p.y;
      else if (p.y > r.y + r.height) dy = p.y - (r.y + r.height);
      var score = dx * dx + dy * dy;
      if (bestScore === null || score < bestScore) { bestScore = score; bestKey = screens[j].key; }
    }
    return bestKey;
  };

  LayoutEngine.prototype._zsKey = function (areaKey, zoneId) {
    return "" + areaKey + "::" + zoneId;
  };

  LayoutEngine.prototype.reloadConfig = function () {
    this.state.load();
    this.layout = null;
    this.layoutId = null;
    // ensure layouts are loaded even if no window is active yet
    this._ensureLayoutsLoaded();
    this.ensureLayout();
    this.reflowAll();
    if (this.state.config.captureOnConfigChange) this.captureAll({ includeAssigned: false });
  };

  LayoutEngine.prototype._zoneRect = function (zone, area) {
    var nx = clamp(zone.rect.x, 0, 1);
    var ny = clamp(zone.rect.y, 0, 1);
    var nw = clamp(zone.rect.w, 0, 1);
    var nh = clamp(zone.rect.h, 0, 1);

    // Optional top reservation (px) for a custom header/tab bar, applied to selected zones.
    try {
      var headerPx2 = (this.state && this.state.config) ? (+this.state.config.mainHeaderPx || 0) : 0;
      if (headerPx2 > 0 && zone && zone.reserveTop && area && area.height > 0) {
        var frac = headerPx2 / area.height;
        frac = clamp(frac, 0, 0.5);
        ny = frac + (1 - frac) * ny;
        nh = (1 - frac) * nh;
      }
    } catch (eH2) {
      warnOnce("zoneRect.reserveTop", "Failed to apply reserved top space", eH2);
    }

    var x = Math.round(area.x + area.width * nx);
    var y = Math.round(area.y + area.height * ny);
    var w = Math.round(area.width * nw);
    var h = Math.round(area.height * nh);

    // Optional pixel insets (used by some Stack layouts, e.g. bottom margin on side columns).
    var insets = (zone && zone.insetsPx) ? zone.insetsPx : null;
    if (insets) {
      var il = 0, it = 0, ir = 0, ib = 0;
      il = Math.max(0, Math.round(+insets.l || 0));
      it = Math.max(0, Math.round(+insets.t || 0));
      ir = Math.max(0, Math.round(+insets.r || 0));
      ib = Math.max(0, Math.round(+insets.b || 0));
      x += il; y += it; w -= (il + ir); h -= (it + ib);
    }

    // Keep within the reported client area (avoid bleeding under panels or off-screen).
    x = clamp(x, area.x, area.x + area.width - 1);
    y = clamp(y, area.y, area.y + area.height - 1);
    w = Math.max(1, Math.min(w, (area.x + area.width) - x));
    h = Math.max(1, Math.min(h, (area.y + area.height) - y));

    return rect(x, y, w, h);
  };

  LayoutEngine.prototype._findCandidateZone = function (point, layout, area) {
    if (!layout) return null;
    var best = null;
    var bestArea = null;
    var bestDist = null;

    for (var i = 0; i < layout.zones.length; i++) {
      var zone = layout.zones[i];
      if (!zone || !zone.id || !zone.rect) continue;
      var zr = this._zoneRect(zone, area);
      var inside = pointInRect(point, zr);
      var areaVal = zr.width * zr.height;
      var c = rectCenter(zr);
      var d2 = dist2(point, c);
      if (inside) {
        if (best === null || areaVal < bestArea) {
          best = zone;
          bestArea = areaVal;
          bestDist = d2;
        } else if (areaVal === bestArea && d2 < bestDist) {
          best = zone;
          bestDist = d2;
        }
      } else {
        if (best === null) {
          best = zone;
          bestDist = d2;
          bestArea = areaVal;
        } else if (bestArea !== null && bestDist !== null && bestDist > d2 && bestArea === null) {
          best = zone;
          bestDist = d2;
        }
      }
    }

    return best;
  };

  LayoutEngine.prototype._resolveTargetZoneId = function (zone) {
    if (!zone) return null;
    if (zone.isDropZone && zone.targetZoneId) return zone.targetZoneId;
    return zone.id;
  };

  LayoutEngine.prototype._ensureClientTracked = function (c) {
    var key = clientKey(c);
    this.clientsByKey[key] = c;
    return key;
  };

  LayoutEngine.prototype._assigned = function (c) {
    var key = clientKey(c);
    return this.state.state.assignments[key] || null;
  };

  LayoutEngine.prototype._normalizeZoneIdForLayout = function (layout, zoneId) {
    if (!layout) return null;
    if (zoneId && layout._zoneById && layout._zoneById[zoneId] && !layout._zoneById[zoneId].isDropZone) return zoneId;
    if (layout._zoneById && layout._zoneById["main"] && !layout._zoneById["main"].isDropZone) return "main";
    for (var i = 0; i < layout.zones.length; i++) {
      var z = layout.zones[i];
      if (z && z.id && !z.isDropZone) return z.id;
    }
    return null;
  };

  LayoutEngine.prototype._reconcileAssignmentForClient = function (c) {
    if (!c || !isManageableClient(c)) return;
    var key = clientKey(c);
    var st = this.state.state.assignments && this.state.state.assignments[key];
    if (!st || !st.zoneId) return;

    var ctx = this._ctxForClient(c);
    if (!ctx.layout) return;

    var prevAreaKey = st.areaKey || ctx.areaKey;
    var prevZoneId = st.zoneId;
    var desiredZone = this._normalizeZoneIdForLayout(ctx.layout, st.zoneId);
    if (desiredZone && desiredZone !== st.zoneId) st.zoneId = desiredZone;

    // Move to correct areaKey if the window has moved between screens.
    if (!st.areaKey) st.areaKey = ctx.areaKey;
    if (!st.layoutId) st.layoutId = ctx.layoutId;
    if (st.areaKey !== ctx.areaKey) {
      this._zoneStacksRemove(prevAreaKey, prevZoneId, key);
      st.areaKey = ctx.areaKey;
      st.layoutId = ctx.layoutId;
      this._zoneStacksAppend(st.areaKey, st.zoneId, key);
    }
  };

  LayoutEngine.prototype.attachClientToZone = function (c, zoneId, opts) {
    if (!isManageableClient(c)) return false;
    var options = opts || {};
    var doReflow = options.reflow !== false;

    var ctx = this._ctxForClient(c);
    if (!ctx.layout) return false;
    var normalizedZoneId = this._normalizeZoneIdForLayout(ctx.layout, zoneId);
    if (!normalizedZoneId) return false;

    var key = this._ensureClientTracked(c);

    var existing = this.state.state.assignments[key];
    if (!existing) {
      existing = { zoneId: null, restore: rectToObj(c.frameGeometry), screen: getClientScreen(c) };
    }
    var prevZoneId = existing.zoneId;
    var prevAreaKey = existing.areaKey || ctx.areaKey;
    existing.zoneId = normalizedZoneId;
    existing.screen = getClientScreen(c);
    existing.areaKey = ctx.areaKey;
    existing.layoutId = ctx.layoutId;
    this.state.state.assignments[key] = existing;

    this._zoneStacksEnsure(ctx.areaKey, normalizedZoneId);
    if (prevZoneId && (prevZoneId !== normalizedZoneId || prevAreaKey !== ctx.areaKey)) {
      this._zoneStacksRemove(prevAreaKey, prevZoneId, key);
    }
    this._zoneStacksAppend(ctx.areaKey, normalizedZoneId, key);
    this.state.save();

    if (doReflow) this.reflowZone(ctx.areaKey, ctx.layout, ctx.area, normalizedZoneId);
    return true;
  };

  LayoutEngine.prototype.attachClientToZoneWithContext = function (c, zoneId, ctx, opts) {
    if (!isManageableClient(c)) return false;
    var options = opts || {};
    var doReflow = options.reflow !== false;
    if (!ctx || !ctx.layout || !ctx.area) return false;

    var normalizedZoneId = this._normalizeZoneIdForLayout(ctx.layout, zoneId);
    if (!normalizedZoneId) return false;

    var key = this._ensureClientTracked(c);

    var existing = this.state.state.assignments[key];
    if (!existing) {
      existing = { zoneId: null, restore: rectToObj(c.frameGeometry), screen: getClientScreen(c) };
    }
    var prevZoneId = existing.zoneId;
    var prevAreaKey = existing.areaKey || ctx.areaKey;
    existing.zoneId = normalizedZoneId;
    existing.screen = getClientScreen(c);
    existing.areaKey = ctx.areaKey;
    existing.layoutId = ctx.layoutId;
    this.state.state.assignments[key] = existing;

    this._zoneStacksEnsure(ctx.areaKey, normalizedZoneId);
    if (prevZoneId && (prevZoneId !== normalizedZoneId || prevAreaKey !== ctx.areaKey)) {
      this._zoneStacksRemove(prevAreaKey, prevZoneId, key);
    }
    this._zoneStacksAppend(ctx.areaKey, normalizedZoneId, key);
    this.state.save();

    if (doReflow) this.reflowZone(ctx.areaKey, ctx.layout, ctx.area, normalizedZoneId);
    return true;
  };

  LayoutEngine.prototype._zoneStacksEnsure = function (areaKey, zoneId) {
    if (!this.state.state.zoneStacks) this.state.state.zoneStacks = {};
    var k = this._zsKey(areaKey, zoneId);
    if (!this.state.state.zoneStacks[k]) this.state.state.zoneStacks[k] = [];
  };

  LayoutEngine.prototype._zoneStacksRemove = function (areaKey, zoneId, key) {
    this._zoneStacksEnsure(areaKey, zoneId);
    var arr = this.state.state.zoneStacks[this._zsKey(areaKey, zoneId)];
    for (var i = arr.length - 1; i >= 0; i--) {
      if (arr[i] === key) arr.splice(i, 1);
    }
  };

  LayoutEngine.prototype._zoneStacksAppend = function (areaKey, zoneId, key) {
    this._zoneStacksEnsure(areaKey, zoneId);
    this._zoneStacksRemove(areaKey, zoneId, key);
    this.state.state.zoneStacks[this._zsKey(areaKey, zoneId)].push(key);
  };

  LayoutEngine.prototype.detachClient = function (c) {
    var key = clientKey(c);
    var st = this.state.state.assignments[key];
    if (!st) return false;
    if (st.zoneId) this._zoneStacksRemove(st.areaKey || "", st.zoneId, key);
    delete this.state.state.assignments[key];
    this.state.save();

    try {
      if (st.restore) {
        c.frameGeometry = rect(st.restore.x, st.restore.y, st.restore.width, st.restore.height);
      }
    } catch (e) {
      warnOnce("detachClient.restore", "Failed to restore window geometry for detach", e);
    }
    this.reflowAll();
    return true;
  };

  LayoutEngine.prototype._clientsInZone = function (areaKey, zoneId) {
    var out = [];
    var assignments = this.state.state.assignments || {};
    var zsKey = this._zsKey(areaKey, zoneId);
    var stacks = (this.state.state.zoneStacks && this.state.state.zoneStacks[zsKey]) ? this.state.state.zoneStacks[zsKey] : [];

    // 1) Prefer explicit stack ordering
    for (var i = 0; i < stacks.length; i++) {
      var key = stacks[i];
      var st = assignments[key];
      if (!st || st.zoneId !== zoneId) continue;
      if ((st.areaKey || "") !== (areaKey || "")) continue;
      var c = this.clientsByKey[key];
      if (c && isManageableClient(c)) out.push(c);
    }

    // 2) Append any assigned but missing from stack list
    for (var k in assignments) {
      if (!assignments[k] || assignments[k].zoneId !== zoneId) continue;
      if (((assignments[k].areaKey || "") !== (areaKey || ""))) continue;
      var present = false;
      for (var j = 0; j < stacks.length; j++) if (stacks[j] === k) present = true;
      if (present) continue;
      var c2 = this.clientsByKey[k];
      if (c2 && isManageableClient(c2)) out.push(c2);
      this._zoneStacksAppend(areaKey, zoneId, k);
    }

    return out;
  };

  LayoutEngine.prototype.reflowZone = function (areaKey, layout, area, zoneId) {
    if (!layout || !layout._zoneById) return;
    var zone = layout._zoneById[zoneId];
    if (!zone) return;

    var clients = this._clientsInZone(areaKey, zoneId);
    if (clients.length === 0) return;

    var zr = this._zoneRect(zone, area);

    var mode = zone.mode || "stack";
    if (mode === "tileVertical") {
      var h = Math.floor(zr.height / clients.length);
      for (var i = 0; i < clients.length; i++) {
        try {
          clients[i].frameGeometry = rect(zr.x, zr.y + i * h, zr.width, i === clients.length - 1 ? (zr.height - i * h) : h);
        } catch (e1) {
          warnOnce("reflowZone.setGeometry.tileVertical." + clientKey(clients[i]), "Failed to set frameGeometry (tileVertical)", e1);
        }
      }
    } else if (mode === "tileHorizontal") {
      var w = Math.floor(zr.width / clients.length);
      for (var j = 0; j < clients.length; j++) {
        try {
          clients[j].frameGeometry = rect(zr.x + j * w, zr.y, j === clients.length - 1 ? (zr.width - j * w) : w, zr.height);
        } catch (e2) {
          warnOnce("reflowZone.setGeometry.tileHorizontal." + clientKey(clients[j]), "Failed to set frameGeometry (tileHorizontal)", e2);
        }
      }
    } else {
      // stack
      for (var k = 0; k < clients.length; k++) {
        try {
          clients[k].frameGeometry = zr;
        } catch (e3) {
          warnOnce("reflowZone.setGeometry.stack." + clientKey(clients[k]), "Failed to set frameGeometry (stack)", e3);
        }
      }
    }
  };

  LayoutEngine.prototype.reflowAll = function () {
    this._ensureLayoutsLoaded();

    // Reconcile assignments (areaKey/layout) for currently tracked windows.
    try {
      var clients = null;
      if (workspace.windowList) clients = workspace.windowList();
      else if (workspace.clientList) clients = workspace.clientList();
      if (clients) {
        for (var i0 = 0; i0 < clients.length; i0++) {
          var c0 = clients[i0];
          if (!isManageableClient(c0)) continue;
          this._ensureClientTracked(c0);
          this._reconcileAssignmentForClient(c0);
        }
      }
    } catch (e0) { warnOnce("reflowAll.reconcile", "Failed to reconcile assignments", e0); }

    // Group by areaKey (screen geometry) and reflow using that screen's layout.
    var areaMap = {};
    var assignments = this.state.state.assignments || {};
    for (var key in assignments) {
      var st = assignments[key];
      if (!st || !st.zoneId) continue;
      var c = this.clientsByKey[key];
      if (!c || !isManageableClient(c)) continue;
      var ctx = this._ctxForClient(c);
      if (!ctx.layout) continue;
      areaMap[ctx.areaKey] = ctx;
      // keep assignment aligned with computed ctx
      st.areaKey = ctx.areaKey;
      st.layoutId = ctx.layoutId;
      st.zoneId = this._normalizeZoneIdForLayout(ctx.layout, st.zoneId);
    }

    for (var ak in areaMap) {
      var ctx2 = areaMap[ak];
      var layout = ctx2.layout;
      if (!layout || !layout.zones) continue;
      for (var i = 0; i < layout.zones.length; i++) {
        var z = layout.zones[i];
        if (z && z.id && !z.isDropZone) this.reflowZone(ctx2.areaKey, layout, ctx2.area, z.id);
      }
    }
  };

  LayoutEngine.prototype.captureAll = function (opts) {
    this._ensureLayoutsLoaded();
    opts = opts || {};
    var includeAssigned = !!opts.includeAssigned;
    var clients = null;
    try {
      if (workspace.windowList) clients = workspace.windowList();
      else if (workspace.clientList) clients = workspace.clientList();
    } catch (e0) {
      clients = null;
    }
    if (!clients) return;
    for (var i = 0; i < clients.length; i++) {
      var c = clients[i];
      if (!isManageableClient(c)) continue;
      var key = this._ensureClientTracked(c);
      if (!includeAssigned) {
        try {
          var st0 = this.state && this.state.state && this.state.state.assignments ? this.state.state.assignments[key] : null;
          if (st0 && st0.zoneId) continue;
        } catch (eS0) { warnOnce("captureAll.assigned", "Failed to check existing assignment", eS0); }
      }
      var g = c.frameGeometry;
      var p = rectCenter(g);
      var ctx = this._ctxForClient(c);
      if (!ctx.layout) continue;
      var zone = this._findCandidateZone(p, ctx.layout, ctx.area);
      if (!zone) continue;
      var targetZoneId = this._resolveTargetZoneId(zone);
      if (!ctx.layout._zoneById[targetZoneId]) continue;
      // Avoid reflowing per-window; reflow once at end to reduce lag/freezes.
      this.attachClientToZone(c, targetZoneId, { reflow: false });
    }
    this.reflowAll();
  };

  LayoutEngine.prototype.captureActive = function () {
    this._ensureLayoutsLoaded();
    var c = null;
    try { c = workspace.activeWindow || workspace.activeClient; } catch (e0) { c = null; }
    if (!c || !isManageableClient(c)) return false;
    var g = c.frameGeometry;
    var p = rectCenter(g);
    var ctx = this._ctxForClient(c);
    if (!ctx.layout) return false;
    var zone = this._findCandidateZone(p, ctx.layout, ctx.area);
    if (!zone) return false;
    var targetZoneId = this._resolveTargetZoneId(zone);
    if (!ctx.layout._zoneById[targetZoneId]) return false;
    return this.attachClientToZone(c, targetZoneId, { reflow: true });
  };

  LayoutEngine.prototype.cycleLayoutForActiveScreen = function () {
    this._ensureLayoutsLoaded();
    var c = null;
    try { c = workspace.activeWindow || workspace.activeClient; } catch (e0) { c = null; }
    if (!c || !isManageableClient(c)) return false;

    var ctx = this._ctxForClient(c);
    if (!ctx.areaKey || !ctx.layout) return false;

    if (!this._areaLayoutOverride) this._areaLayoutOverride = {};

    var ids = [];
    for (var id in this.layouts) { if (this.layouts[id]) ids.push(id); }
    ids.sort();
    if (ids.length === 0) return false;

    var current = this._areaLayoutOverride[ctx.areaKey] || this._pickLayoutIdForArea(ctx.area);
    var idx = -1;
    for (var i = 0; i < ids.length; i++) if (ids[i] === current) idx = i;
    var next = ids[(idx >= 0 ? (idx + 1) : 0) % ids.length];

    this._areaLayoutOverride[ctx.areaKey] = next;
    log("Layout override for " + ctx.areaKey + ": " + next);
    this.reflowAll();
    return true;
  };

  LayoutEngine.prototype._showOutline = function (r) {
    try {
      if (workspace && isFn(workspace.showOutline)) workspace.showOutline(r);
    } catch (e) {
      // ignore
    }
  };

  LayoutEngine.prototype._hideOutline = function () {
    try {
      if (workspace && isFn(workspace.hideOutline)) workspace.hideOutline();
    } catch (e) {
      // ignore
    }
  };

  LayoutEngine.prototype.debugToggleOutline = function () {
    var c = null;
    try { c = workspace.activeWindow || workspace.activeClient; } catch (e0) { c = null; }
    if (!c || !isManageableClient(c)) return false;
    try {
      if (!this._debugOutlineOn) {
        this._showOutline(c.frameGeometry);
        this._debugOutlineOn = true;
        log("Debug outline: on");
      } else {
        this._hideOutline();
        this._debugOutlineOn = false;
        log("Debug outline: off");
      }
      return true;
    } catch (e) {
      return false;
    }
  };

  LayoutEngine.prototype._startMoveTracking = function (c) {
    this.ensureLayout();
    this._moveClient = c;
    this._moveCandidateZoneId = null;
    this._updateMoveCandidate();
  };

  LayoutEngine.prototype._updateMoveCandidate = function () {
    if (!this._moveClient) return;
    var c = this._moveClient;
    if (!isManageableClient(c)) return;

    var g = c.frameGeometry;
    var p = rectCenter(g);
    var ctx = this._ctxForClient(c);
    if (!ctx.layout) return;
    var zone = this._findCandidateZone(p, ctx.layout, ctx.area);
    if (!zone) return;

    var targetZoneId = this._resolveTargetZoneId(zone);
    if (targetZoneId !== this._moveCandidateZoneId || ctx.areaKey !== this._moveCandidateAreaKey) {
      this._moveCandidateZoneId = targetZoneId;
      this._moveCandidateAreaKey = ctx.areaKey;
      // show outline for the (drop) zone, not necessarily the target, to better match "drop zones".
      var outlineZone = zone;
      var outlineRect = this._zoneRect(outlineZone, ctx.area);
      this._showOutline(outlineRect);
    }
  };

  LayoutEngine.prototype.debugDumpAreas = function () {
    try {
      var c = null;
      try { c = workspace.activeWindow || workspace.activeClient; } catch (e0) { c = null; }
      if (!c) { log("debugDumpAreas: no active window"); return; }

      var screenIdx = getClientScreen(c);
      var desktop = getCurrentDesktop();
      var p = rectCenter(c.frameGeometry);

      function areaByType(t) {
        try {
          if (!workspace || !isFn(workspace.clientArea)) return null;
          var a = workspace.clientArea(t, screenIdx, desktop);
          return a ? rectToObj(a) : null;
        } catch (e1) { return null; }
      }

      var types = {};
      try {
        if (typeof KWin !== "undefined") {
          if (KWin.MaximizeArea !== undefined) types.max = areaByType(KWin.MaximizeArea);
          if (KWin.WorkArea !== undefined) types.work = areaByType(KWin.WorkArea);
          if (KWin.PlacementArea !== undefined) types.place = areaByType(KWin.PlacementArea);
          if (KWin.FullScreenArea !== undefined) types.fullscreen = areaByType(KWin.FullScreenArea);
          if (KWin.FullArea !== undefined) types.full = areaByType(KWin.FullArea);
        }
      } catch (e2) { warnOnce("debugDumpAreas.types", "Failed to query some raw clientArea types", e2); }

      var cap = {
        caption: c.caption,
        screen: screenIdx,
        desktop: desktop,
        frame: rectToObj(c.frameGeometry),
        point: { x: p.x, y: p.y },
        clientAreaForWindow: rectToObj(clientAreaForWindow(c)),
        clientAreaForPoint: rectToObj(clientAreaForPoint(p)),
        raw: types,
      };
      log("debugDumpAreas: " + safeStringify(cap));
    } catch (e3) {
      log("debugDumpAreas failed: " + e3);
    }
  };

  LayoutEngine.prototype._finishMoveTracking = function (c) {
    this._hideOutline();

    if (!c || c !== this._moveClient) {
      this._moveClient = null;
      this._moveCandidateZoneId = null;
      return;
    }

    var zoneId = this._moveCandidateZoneId;
    this._moveClient = null;
    this._moveCandidateZoneId = null;
    this._moveCandidateAreaKey = null;

    if (zoneId) {
      var ctx = this._ctxForClient(c);
      if (ctx.layout && ctx.layout._zoneById[zoneId]) {
      this.attachClientToZone(c, zoneId);
      this.reflowZone(ctx.areaKey, ctx.layout, ctx.area, zoneId);
      }
    }
  };

  LayoutEngine.prototype._neighborZone = function (layout, area, fromPoint, fromZoneId, direction, allowWrap) {
    // Centroid-based traversal (closer to Stack behavior):
    // - Use centroids of zones, not rectangle adjacency.
    // - Pick the closest centroid in the requested direction (with a penalty for orthogonal distance).
    // - If none are ahead, wrap to the extreme zone along that axis.
    if (!layout || !layout.zones) return null;

    var origin = fromPoint;
    if (!origin && fromZoneId && layout._zoneById && layout._zoneById[fromZoneId]) {
      origin = rectCenter(this._zoneRect(layout._zoneById[fromZoneId], area));
    }
    if (!origin) return null;

    var isHorizontal = direction === "left" || direction === "right";
    var forward = direction === "right" || direction === "down";
    var orthWeight = 3; // larger = more likely to stay in same row/col

    var bestId = null;
    var bestScore = null;
    var bestAxis = null;

    var wrapId = null;
    var wrapAxis = null;
    var wrapOrth = null;

    for (var i = 0; i < layout.zones.length; i++) {
      var z = layout.zones[i];
      if (!z || !z.id || z.isDropZone) continue;
      if (fromZoneId && z.id === fromZoneId) continue;

      var zr = this._zoneRect(z, area);
      var c = rectCenter(zr);
      var axis = isHorizontal ? (c.x - origin.x) : (c.y - origin.y);
      var orth = isHorizontal ? (c.y - origin.y) : (c.x - origin.x);

      // Track wrap candidate (extreme along axis).
      var cAxisAbs = isHorizontal ? c.x : c.y;
      if (wrapId === null) {
        wrapId = z.id;
        wrapAxis = cAxisAbs;
        wrapOrth = Math.abs(orth);
      } else if (forward) {
        if (cAxisAbs < wrapAxis || (cAxisAbs === wrapAxis && Math.abs(orth) < wrapOrth)) {
          wrapId = z.id;
          wrapAxis = cAxisAbs;
          wrapOrth = Math.abs(orth);
        }
      } else {
        if (cAxisAbs > wrapAxis || (cAxisAbs === wrapAxis && Math.abs(orth) < wrapOrth)) {
          wrapId = z.id;
          wrapAxis = cAxisAbs;
          wrapOrth = Math.abs(orth);
        }
      }

      // Forward candidate scoring.
      if (forward ? (axis <= 0) : (axis >= 0)) continue;
      var axisAbs = Math.abs(axis);
      var score = axisAbs * axisAbs + (Math.abs(orth) * orthWeight) * (Math.abs(orth) * orthWeight);

      if (bestScore === null || score < bestScore) {
        bestScore = score;
        bestId = z.id;
        bestAxis = axisAbs;
      } else if (score === bestScore && bestAxis !== null && axisAbs < bestAxis) {
        // deterministic tie-break
        bestId = z.id;
        bestAxis = axisAbs;
      }
    }

    if (bestId) return bestId;
    return allowWrap ? wrapId : null;
  };

  LayoutEngine.prototype._globalZoneIndex = function () {
    // Build a global list of all non-drop zones across all known screens.
    // This allows navigation in a single virtual 2D space (Stack-like), not per-screen.
    this._ensureLayoutsLoaded();

    var screens = this._screensForTraversal();
    if (!screens || screens.length === 0) return [];

    var out = [];
    for (var i = 0; i < screens.length; i++) {
      var sk = screens[i].key;
      var sr = screens[i].rect;
      if (!sk || !sr) continue;

      // Prefer KWin's actual usable area (respects panels/struts).
      var probe = { x: sr.x + Math.round(sr.width / 2), y: sr.y + Math.round(sr.height / 2) };
      var area = clientAreaForPoint(probe) || sr;
      if (!area || area.width <= 0 || area.height <= 0) continue;

      var layoutId = this._pickLayoutIdForArea(area, sk);
      if (isDisabledLayoutId(layoutId)) continue;
      var layout = this.layouts[layoutId] || this.layouts[this.state.config.defaultLayoutId] || null;
      if (!layout) continue;

      var areaKey = this._layoutKeyForArea(area);
      for (var j = 0; j < layout.zones.length; j++) {
        var z = layout.zones[j];
        if (!z || !z.id || z.isDropZone) continue;
        var zr = this._zoneRect(z, area);
        out.push({
          screenKey: sk,
          area: area,
          areaKey: areaKey,
          layoutId: layoutId,
          layout: layout,
          zoneId: z.id,
          rect: zr,
          center: rectCenter(zr),
        });
      }
    }

    return out;
  };

  LayoutEngine.prototype._globalNeighborZone = function (zones, fromPoint, fromIdentity, direction) {
    if (!zones || zones.length === 0 || !fromPoint) return null;
    var isHorizontal = direction === "left" || direction === "right";
    var forward = direction === "right" || direction === "down";
    var orthWeight = 3;

    var best = null;
    var bestScore = null;
    for (var i = 0; i < zones.length; i++) {
      var z = zones[i];
      if (!z || !z.center) continue;
      if (fromIdentity && z.screenKey === fromIdentity.screenKey && z.zoneId === fromIdentity.zoneId && z.areaKey === fromIdentity.areaKey) continue;

      var axis = isHorizontal ? (z.center.x - fromPoint.x) : (z.center.y - fromPoint.y);
      var orth = isHorizontal ? (z.center.y - fromPoint.y) : (z.center.x - fromPoint.x);
      if (forward ? axis <= 0 : axis >= 0) continue;

      var axisAbs = Math.abs(axis);
      var score = axisAbs * axisAbs + (Math.abs(orth) * orthWeight) * (Math.abs(orth) * orthWeight);
      if (bestScore === null || score < bestScore) { bestScore = score; best = z; }
    }
    return best;
  };

  LayoutEngine.prototype._snapToEdgeZoneOnTargetScreen = function (zones, fromPoint, targetScreenKey, direction) {
    if (!zones || zones.length === 0 || !fromPoint || !targetScreenKey) return null;
    var best = null;
    var bestAxis = null;
    var bestOrth = null;

    for (var i = 0; i < zones.length; i++) {
      var z = zones[i];
      if (!z || !z.center || z.screenKey !== targetScreenKey) continue;

      var axis = null;
      var orth = null;
      if (direction === "left") { axis = z.center.x; orth = Math.abs(z.center.y - fromPoint.y); if (z.center.x >= fromPoint.x) continue; }
      else if (direction === "right") { axis = z.center.x; orth = Math.abs(z.center.y - fromPoint.y); if (z.center.x <= fromPoint.x) continue; }
      else if (direction === "up") { axis = z.center.y; orth = Math.abs(z.center.x - fromPoint.x); if (z.center.y >= fromPoint.y) continue; }
      else if (direction === "down") { axis = z.center.y; orth = Math.abs(z.center.x - fromPoint.x); if (z.center.y <= fromPoint.y) continue; }
      else continue;

      if (best === null) {
        best = z;
        bestAxis = axis;
        bestOrth = orth;
        continue;
      }

      if (direction === "left" || direction === "up") {
        // choose the zone closest to the border in that direction on the target screen (max axis),
        // then the one closest orthogonally.
        if (axis > bestAxis || (axis === bestAxis && orth < bestOrth)) {
          best = z; bestAxis = axis; bestOrth = orth;
        }
      } else {
        // right/down: pick min axis
        if (axis < bestAxis || (axis === bestAxis && orth < bestOrth)) {
          best = z; bestAxis = axis; bestOrth = orth;
        }
      }
    }
    return best;
  };

  LayoutEngine.prototype.moveActiveToNeighbor = function (direction) {
    this._ensureLayoutsLoaded();
    var c = null;
    try { c = workspace.activeWindow || workspace.activeClient; } catch (e0) { c = null; }
    if (!c || !isManageableClient(c)) return;
    var st = this._assigned(c);
    var ctx = this._ctxForClient(c);
    if (!ctx.layout) return;

    var fromPoint = null;
    var fromZoneId = null;
    // An assigned tile can occupy only part of its zone. Navigate from the zone center so
    // every window in a vertical/horizontal stack has the same directional neighbors.
    if (st && st.zoneId && ctx.layout._zoneById && ctx.layout._zoneById[st.zoneId]) {
      fromZoneId = st.zoneId;
      fromPoint = rectCenter(this._zoneRect(ctx.layout._zoneById[fromZoneId], ctx.area));
    }
    // Unassigned windows still navigate from their actual position.
    if (!fromPoint) {
      try { fromPoint = rectCenter(c.frameGeometry); } catch (eFP) { fromPoint = null; }
    }
    if (!fromPoint) return;

    // Prefer global traversal across all zones in the virtual 2D space.
    var globalZones = this._globalZoneIndex();
    var fromIdentity = null;
    if (st && st.zoneId) {
      fromIdentity = { screenKey: ctx.screenKey || null, areaKey: ctx.areaKey, zoneId: st.zoneId };
    }
    var target = this._globalNeighborZone(globalZones, fromPoint, fromIdentity, direction);
    if (target) {
      log("Move " + direction + " => " + target.screenKey + ":" + target.zoneId);
      var targetCtx = {
        area: target.area,
        areaKey: target.areaKey,
        screenKey: target.screenKey,
        layoutId: target.layoutId,
        layout: target.layout,
      };
      this.attachClientToZoneWithContext(c, target.zoneId, targetCtx, { reflow: true });
      return;
    }

    // If global traversal can't decide (e.g. only one screen key known), fall back to same-screen traversal.
    var neighbor = this._neighborZone(ctx.layout, ctx.area, fromPoint, fromZoneId, direction, false /* no wrap */);
    if (!neighbor) return;
    this.attachClientToZone(c, neighbor);
    this.reflowZone(ctx.areaKey, ctx.layout, ctx.area, neighbor);
  };

  LayoutEngine.prototype.listZones = function () {
    this.ensureLayout();
    var out = [];
    for (var i = 0; i < this.layout.zones.length; i++) {
      var z = this.layout.zones[i];
      if (!z || !z.id) continue;
      out.push({
        id: z.id,
        name: z.name || z.id,
        mode: z.mode || "stack",
        isDropZone: !!z.isDropZone,
        targetZoneId: z.targetZoneId || null,
        rect: z.rect,
      });
    }
    return out;
  };

  LayoutEngine.prototype.listTabGroups = function () {
    this.ensureLayout();
    var groups = this.layout.tabGroups || [];
    var out = [];
    for (var i = 0; i < groups.length; i++) {
      var g = groups[i];
      if (!g || !g.id) continue;
      out.push({ id: g.id, name: g.name || g.id, zones: g.zones || [] });
    }
    return out;
  };

  LayoutEngine.prototype._groupZoneIds = function (layout, groupId) {
    var groups = (layout && layout.tabGroups) ? layout.tabGroups : [];
    for (var i = 0; i < groups.length; i++) {
      var g = groups[i];
      if (g && g.id === groupId) return g.zones || [];
    }
    return [];
  };

  LayoutEngine.prototype.listWindows = function () {
    this._ensureLayoutsLoaded();
    var out = [];
    var assignments = this.state.state.assignments;
    for (var key in assignments) {
      var st = assignments[key];
      var c = this.clientsByKey[key];
      if (!c || !isManageableClient(c)) continue;
      out.push({
        key: key,
        caption: c.caption,
        zoneId: st.zoneId || null,
        screen: st.screen,
        areaKey: st.areaKey || null,
        layoutId: st.layoutId || null,
        desktop: c.desktop,
        geometry: rectToObj(c.frameGeometry),
      });
    }
    return out;
  };

  LayoutEngine.prototype.listGroupWindows = function (groupId) {
    var a = null;
    try { a = workspace.activeWindow || workspace.activeClient; } catch (e0) { a = null; }
    var ctx = a ? this._ctxForClient(a) : null;
    var zoneIds = ctx && ctx.layout ? this._groupZoneIds(ctx.layout, groupId) : [];
    if (!zoneIds || zoneIds.length === 0) return [];
    var all = this.listWindows();
    var out = [];
    for (var i = 0; i < all.length; i++) {
      var w = all[i];
      if (zoneIds.indexOf(w.zoneId) >= 0) out.push(w);
    }
    return out;
  };

  LayoutEngine.prototype.activateWindow = function (key) {
    var c = this.clientsByKey[key];
    if (!c) return false;
    try {
      try { workspace.activeWindow = c; } catch (e0) { warnOnce("activateWindow.activeWindow", "Failed to set workspace.activeWindow", e0); }
      try { workspace.activeClient = c; } catch (e1) { warnOnce("activateWindow.activeClient", "Failed to set workspace.activeClient", e1); }
      return true;
    } catch (e) {
      warnOnce("activateWindow", "Failed to activate window via workspace.active*", e);
      try {
        if (isFn(workspace.activateClient)) {
          workspace.activateClient(c);
          return true;
        }
      } catch (e2) { warnOnce("activateWindow.activateClient", "workspace.activateClient failed", e2); }
    }
    try { c.active = true; return true; } catch (e3) { warnOnce("activateWindow.clientActive", "Failed to set client.active", e3); }
    return false;
  };

  LayoutEngine.prototype._onClientAdded = function (c) {
    if (!isManageableClient(c)) return;
    var key = this._ensureClientTracked(c);
    // Don't auto-attach new clients by default; user can run CaptureAll.
    // But if it was in state (session restore), reflow its zone.
    var st = this.state.state.assignments[key];
    if (st && st.zoneId) {
      this._reconcileAssignmentForClient(c);
      var ctx = this._ctxForClient(c);
      if (ctx.layout) this.reflowZone(st.areaKey || ctx.areaKey, ctx.layout, ctx.area, st.zoneId);
    }

    // Auto-place info panes: title "StackPane:<zoneId>:<anything>"
    try {
      var cap = "" + (c.caption || "");
      if (cap.indexOf("StackPane:") === 0) {
        var rest = cap.substring("StackPane:".length);
        var zoneId = rest.split(":")[0];
        if (zoneId) this.attachClientToZone(c, zoneId);
        return;
      }
    } catch (e0) {
      warnOnce("onClientAdded.stackPane", "Failed to auto-place StackPane window", e0);
    }

    // Auto-capture new windows (Stack's CaptureOnAppStart), but never fullscreen.
    try {
      if (this._suppressAutoCapture) return;
      if (!this.state || !this.state.config || !this.state.config.autoCaptureOnNewWindow) return;
      if (isFullscreenClient(c)) return;
      if (isParentedLikeClient(c)) return;

      if (matchesAnyIgnoreFilter(c, this.state.config.autoCaptureIgnoreFilters)) return;

      // Only capture when unassigned.
      var st2 = this.state.state.assignments && this.state.state.assignments[key];
      if (st2 && st2.zoneId) return;

      var ctx2 = this._ctxForClient(c);
      if (!ctx2 || !ctx2.layout) return;

      var desired = ("" + (this.state.config.autoCaptureDefaultZoneId || "main")).trim();
      var zone = null;
      if (desired && ctx2.layout._zoneById && ctx2.layout._zoneById[desired] && !ctx2.layout._zoneById[desired].isDropZone) zone = desired;
      else if (ctx2.layout._zoneById && ctx2.layout._zoneById["main"] && !ctx2.layout._zoneById["main"].isDropZone) zone = "main";
      else {
        // first non-drop zone
        for (var zi = 0; zi < ctx2.layout.zones.length; zi++) {
          var z = ctx2.layout.zones[zi];
          if (z && z.id && !z.isDropZone) { zone = z.id; break; }
        }
      }
      if (!zone) return;

      // Avoid disturbing already-captured windows when auto-capturing into a stack zone:
      // - For "stack" mode, only size the new window to the zone rect and bring it forward.
      // - For tiling modes, a reflow is expected.
      var zoneObj = (ctx2.layout && ctx2.layout._zoneById) ? ctx2.layout._zoneById[zone] : null;
      var mode = zoneObj ? (zoneObj.mode || "stack") : "stack";
      var doReflow = (mode === "tileVertical" || mode === "tileHorizontal");

      this.attachClientToZoneWithContext(c, zone, ctx2, { reflow: doReflow });
      if (!doReflow && zoneObj) {
        try { c.frameGeometry = this._zoneRect(zoneObj, ctx2.area); } catch (eG) { warnOnce("autocapture.setGeometry", "Failed to place auto-captured window", eG); }
        try {
          if (isFn(workspace.raiseWindow)) workspace.raiseWindow(c);
          else if (isFn(workspace.raiseClient)) workspace.raiseClient(c);
        } catch (eR) { warnOnce("autocapture.raise", "Failed to raise auto-captured window", eR); }
      }
    } catch (e1) {
      warnOnce("onClientAdded.autocapture", "Auto-capture failed", e1);
    }
  };

  LayoutEngine.prototype._onClientRemoved = function (c) {
    var key = clientKey(c);
    delete this.clientsByKey[key];
    // Leave assignment in state for session restore (best-effort).
  };

  LayoutEngine.prototype._onClientActivated = function (c) {
    // Keep stack ordering simple: raise active window.
    if (!c || !isManageableClient(c)) return;
    this._reconcileAssignmentForClient(c);
    var key = clientKey(c);
    var st = this.state.state.assignments && this.state.state.assignments[key];
    if (st && st.zoneId) {
      this._zoneStacksAppend(st.areaKey || "", st.zoneId, key);
      this.state.save();
    }
    try {
      if (isFn(workspace.raiseWindow)) workspace.raiseWindow(c);
      else if (isFn(workspace.raiseClient)) workspace.raiseClient(c);
    } catch (e) { warnOnce("onClientActivated.raise", "Failed to raise active window", e); }
  };

  LayoutEngine.prototype._cycleInList = function (keys, forward) {
    if (!keys || keys.length < 2) return false;
    var active = null;
    try { active = workspace.activeWindow || workspace.activeClient; } catch (e0) { active = null; }
    if (!active) return false;
    var activeKey = clientKey(active);
    var idx = -1;
    for (var i = 0; i < keys.length; i++) if (keys[i] === activeKey) idx = i;
    if (idx < 0) return false;
    var nextIdx = forward ? (idx + 1) : (idx - 1);
    if (nextIdx < 0) nextIdx = keys.length - 1;
    if (nextIdx >= keys.length) nextIdx = 0;
    return this.activateWindow(keys[nextIdx]);
  };

  LayoutEngine.prototype.cycleActiveZone = function (forward) {
    this._ensureLayoutsLoaded();
    var c = null;
    try { c = workspace.activeWindow || workspace.activeClient; } catch (e0) { c = null; }
    if (!c) return false;
    var key = clientKey(c);
    var st = this.state.state.assignments && this.state.state.assignments[key];
    if (!st || !st.zoneId) return false;
    var zoneId = st.zoneId;
    var areaKey = st.areaKey || this._ctxForClient(c).areaKey;
    this._zoneStacksEnsure(areaKey, zoneId);
    return this._cycleInList(this.state.state.zoneStacks[this._zsKey(areaKey, zoneId)], !!forward);
  };

  LayoutEngine.prototype.cycleTabGroup = function (groupId, forward) {
    this._ensureLayoutsLoaded();
    var active = null;
    try { active = workspace.activeWindow || workspace.activeClient; } catch (e0) { active = null; }
    if (!active) return false;
    var ctx = this._ctxForClient(active);
    var zoneIds = this._groupZoneIds(ctx.layout, groupId);
    if (!zoneIds || zoneIds.length === 0) return false;
    var keys = [];
    for (var i = 0; i < zoneIds.length; i++) {
      this._zoneStacksEnsure(ctx.areaKey, zoneIds[i]);
      var arr = this.state.state.zoneStacks[this._zsKey(ctx.areaKey, zoneIds[i])];
      for (var j = 0; j < arr.length; j++) keys.push(arr[j]);
    }
    // de-dup in order
    var seen = {};
    var uniq = [];
    for (var k = 0; k < keys.length; k++) {
      if (seen[keys[k]]) continue;
      seen[keys[k]] = true;
      uniq.push(keys[k]);
    }
    return this._cycleInList(uniq, !!forward);
  };

  LayoutEngine.prototype.init = function () {
    this.ensureLayout();

    var self = this;

    function connectSignal(obj, name, handler) {
      try {
        if (obj && obj[name] && isFn(obj[name].connect)) {
          obj[name].connect(handler);
          return true;
        }
      } catch (e) { warnOnce("connectSignal." + name, "Failed to connect signal: " + name, e); }
      return false;
    }

    function hookMoveSignals(win) {
      if (!win) return;
      connectSignal(win, "windowStartUserMovedResized", function () { if (isManageableClient(win)) self._startMoveTracking(win); });
      connectSignal(win, "windowFinishUserMovedResized", function () { if (isManageableClient(win)) self._finishMoveTracking(win); });
      connectSignal(win, "windowStepUserMovedResized", function () { if (self._moveClient && win === self._moveClient) self._updateMoveCandidate(); });
      connectSignal(win, "clientStepUserMovedResized", function () { if (self._moveClient && win === self._moveClient) self._updateMoveCandidate(); });
      connectSignal(win, "frameGeometryChanged", function () { if (self._moveClient && win === self._moveClient) self._updateMoveCandidate(); });
    }

    connectSignal(workspace, "windowAdded", function (w) { self._onClientAdded(w); hookMoveSignals(w); });
    connectSignal(workspace, "windowRemoved", function (w) { self._onClientRemoved(w); });
    connectSignal(workspace, "windowActivated", function (w) { self._onClientActivated(w); });

    // Fallback names
    connectSignal(workspace, "clientAdded", function (w) { self._onClientAdded(w); hookMoveSignals(w); });
    connectSignal(workspace, "clientRemoved", function (w) { self._onClientRemoved(w); });
    connectSignal(workspace, "clientActivated", function (w) { self._onClientActivated(w); });

    // Track existing windows
    try {
      var clients = null;
      if (workspace.windowList) clients = workspace.windowList();
      else if (workspace.clientList) clients = workspace.clientList();
      if (clients) {
        // Suppress auto-capture for existing windows on (re)load.
        this._suppressAutoCapture = true;
        for (var i = 0; i < clients.length; i++) { this._onClientAdded(clients[i]); hookMoveSignals(clients[i]); }
        this._suppressAutoCapture = false;
      }
    } catch (e2) { warnOnce("init.trackExisting", "Failed to enumerate existing windows", e2); }

    // Shortcuts (safe defaults; can be changed in KWin shortcut settings)
    try {
      registerShortcut("StackCaptureAll", "Stack: Capture all windows", "Meta+Ctrl+J", function () {
        // Stack-like behavior: only capture *uncaptured* windows by default.
        self.captureAll({ includeAssigned: false });
      });
      registerShortcut("StackReCaptureAll", "Stack: Re-capture all windows (force)", "Meta+Ctrl+Shift+J", function () {
        self.captureAll({ includeAssigned: true });
      });
      registerShortcut("StackCaptureActive", "Stack: Capture active window", "Meta+Ctrl+K", function () {
        self.captureActive();
      });
      registerShortcut("StackSelectLayout", "Stack: Cycle layout (current screen)", "Meta+Ctrl+L", function () {
        self.cycleLayoutForActiveScreen();
      });
      registerShortcut("StackDebugToggleOutline", "Stack: Debug toggle outline (active window)", "Meta+Ctrl+Alt+O", function () {
        self.debugToggleOutline();
      });
      registerShortcut("StackDebugDumpAreas", "Stack: Debug dump client areas (active window)", "Meta+Ctrl+Alt+A", function () {
        self.debugDumpAreas();
      });
      registerShortcut("StackDetach", "Stack: Detach active window", "Meta+Escape", function () {
        var c = null;
        try { c = workspace.activeWindow || workspace.activeClient; } catch (e0) { c = null; }
        if (c) self.detachClient(c);
      });
      registerShortcut("StackReloadConfig", "Stack: Reload config", "Meta+Ctrl+R", function () {
        self.reloadConfig();
      });
      registerShortcut("StackCycleZoneNext", "Stack: Cycle active zone (next)", "Meta+Ctrl+Tab", function () {
        self.cycleActiveZone(true);
      });
      registerShortcut("StackCycleZonePrev", "Stack: Cycle active zone (prev)", "Meta+Ctrl+Shift+Tab", function () {
        self.cycleActiveZone(false);
      });
      registerShortcut("StackTabsMainNext", "Stack: Tabs main (next)", "Meta+Ctrl+]", function () {
        self.cycleTabGroup("main", true);
      });
      registerShortcut("StackTabsMainPrev", "Stack: Tabs main (prev)", "Meta+Ctrl+[", function () {
        self.cycleTabGroup("main", false);
      });
      registerShortcut("StackMoveLeft", "Stack: Move active window left", "Meta+Left", function () {
        self.moveActiveToNeighbor("left");
      });
      registerShortcut("StackMoveRight", "Stack: Move active window right", "Meta+Right", function () {
        self.moveActiveToNeighbor("right");
      });
      registerShortcut("StackMoveUp", "Stack: Move active window up", "Meta+Up", function () {
        self.moveActiveToNeighbor("up");
      });
      registerShortcut("StackMoveDown", "Stack: Move active window down", "Meta+Down", function () {
        self.moveActiveToNeighbor("down");
      });
    } catch (e3) {
      warnOnce("init.shortcuts", "Failed to register one or more shortcuts", e3);
    }

    log("Initialized");
  };

  // Bootstrap
  var state = new StackState();
  state.load();

  var engine = new LayoutEngine(state);
  engine.init();

  try {
    if (state.config.captureOnStart) engine.captureAll({ includeAssigned: false });
  } catch (e1) {
    warnOnce("bootstrap.captureOnStart", "captureOnStart failed", e1);
  }

  // React to config changes (kwinrc, shortcuts, etc.)
  try {
    if (options && options.configChanged && isFn(options.configChanged.connect)) {
      options.configChanged.connect(function () {
        engine.reloadConfig();
      });
    }
  } catch (e0) {
    warnOnce("bootstrap.configChanged", "Failed to connect options.configChanged", e0);
  }
})();
