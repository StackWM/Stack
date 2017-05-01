namespace LostTech.Stack.Models.Filters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using LostTech.App;

    public class CommonStringMatchFilter : StringMatchFilter<string>, ICopyable<CommonStringMatchFilter>
    {
        MatchOption match;
        Regex regex;

        public override bool Matches(string value)
        {
            if (value == null)
                return false;

            switch (this.match) {
            case MatchOption.Anywhere:
                return value.Contains(this.Value);
            case MatchOption.Exact:
                return value == this.Value;
            case MatchOption.Prefix:
                return value.StartsWith(this.Value);
            case MatchOption.Suffix:
                return value.EndsWith(this.Value);
            case MatchOption.Regex:
                this.regex = this.regex ?? new Regex(this.Value);
                return this.regex.IsMatch(value);
            default:
                return false;
            }
        }

        public CommonStringMatchFilter Copy() => new CommonStringMatchFilter {
            Match = this.Match,
            Value = this.Value,
        };

        public MatchOption Match {
            get => this.match;
            set {
                if (value == this.match)
                    return;
                this.match = value;
                this.OnPropertyChanged();
            }
        }

        public enum MatchOption
        {
            Exact,
            Prefix,
            Suffix,
            Anywhere,
            Regex,
        }
    }
}
