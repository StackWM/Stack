namespace LostTech.Stack.Utils {
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    internal class RetriableException : Exception {
        public RetriableException() {
        }

        public RetriableException(string message) : base(message) {
        }

        public RetriableException(string message, Exception innerException) : base(message, innerException) {
        }

        public RetriableException(Exception innerException): base("Operation should be retried", innerException) { }

        protected RetriableException(SerializationInfo info, StreamingContext context) : base(info, context) {
        }
    }
}