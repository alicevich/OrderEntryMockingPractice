using System;

namespace OrderEntryMockingPractice.Services
{
    public class ValidationFailedException : Exception
    {
        private readonly string[] _reasons;

        public string[] Reasons { get { return _reasons; } }

        public ValidationFailedException(params string[] reasons)
        {
            _reasons = reasons;
        }
    }
}