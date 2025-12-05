namespace LoggingDemo.Logging
{
    public class MemoryScope
    {
        private static readonly AsyncLocal<Stack<object?>> _scopes = new();

        public static IDisposable Push(object? state)
        {
            if (_scopes.Value == null)
                _scopes.Value = new Stack<object?>();

            _scopes.Value.Push(state);

            return new ScopePopper();
        }

        public static IEnumerable<object?> Current =>
            _scopes.Value ?? Enumerable.Empty<object?>();

        private class ScopePopper : IDisposable
        {
            public void Dispose()
            {
                _scopes.Value?.Pop();
            }
        }
    }
}
