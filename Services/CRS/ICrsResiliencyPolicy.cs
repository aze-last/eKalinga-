using System;
using System.Threading;
using System.Threading.Tasks;

namespace AttendanceShiftingManagement.Services
{
    public interface ICrsResiliencyPolicy
    {
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    }

    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    public class CrsResiliencyPolicy : ICrsResiliencyPolicy
    {
        private static CircuitState _state = CircuitState.Closed;
        private static int _failureCount = 0;
        private static DateTime _lastStateChanged = DateTime.MinValue;
        private static readonly object LockObj = new();

        private const int MaxFailures = 3;
        private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(4); // 4-second timeout

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        {
            CheckCircuitState();

            if (_state == CircuitState.Open)
            {
                throw new InvalidOperationException("CRS service circuit breaker is open. Offline mode active.");
            }

            int attempt = 0;
            const int maxRetries = 1; // single automatic retry policy

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(ExecutionTimeout);

                    T result = await action(timeoutCts.Token);
                    RecordSuccess();
                    return result;
                }
                catch (Exception)
                {
                    attempt++;
                    if (attempt > maxRetries)
                    {
                        RecordFailure();
                        throw;
                    }
                    // Wait briefly before retry
                    await Task.Delay(200, cancellationToken);
                }
            }
        }

        private void CheckCircuitState()
        {
            lock (LockObj)
            {
                if (_state == CircuitState.Open && DateTime.UtcNow - _lastStateChanged > BreakDuration)
                {
                    _state = CircuitState.HalfOpen;
                    _lastStateChanged = DateTime.UtcNow;
                }
            }
        }

        private void RecordSuccess()
        {
            lock (LockObj)
            {
                _failureCount = 0;
                if (_state != CircuitState.Closed)
                {
                    _state = CircuitState.Closed;
                    _lastStateChanged = DateTime.UtcNow;
                }
            }
        }

        private void RecordFailure()
        {
            lock (LockObj)
            {
                _failureCount++;
                if (_failureCount >= MaxFailures)
                {
                    _state = CircuitState.Open;
                    _lastStateChanged = DateTime.UtcNow;
                }
            }
        }

        public CircuitState CurrentState => _state;
        public int FailureCount => _failureCount;
        public DateTime LastStateChanged => _lastStateChanged;
    }
}
