﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/Open-NET-Libraries/Open.Disposable/blob/master/LICENSE.md
 */

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Open.Disposable
{
	public abstract class DeferredCleanupBase : DisposableBase
	{
		public enum CleanupMode
		{
			ImmediateSynchronous, // Cleanup immediately within the current thread.
			ImmediateSynchronousIfPastDue, // Cleanup immediately if time is past due.
			ImmediateDeferred, // Cleanup immediately within another thread.
			ImmediateDeferredIfPastDue, // Cleanup immedidately in another thread if time is past due.
			Deferred // Extend the timer.
		}

		private int _cleanupDelay = 50;
		// So far 50 ms seems optimal...
		public int CleanupDelay
		{
			get => _cleanupDelay;
			set
			{
				if (value < 0)
					throw new ArgumentOutOfRangeException(nameof(value), value, "Cannot be a negative value.");
				_cleanupDelay = value;
			}
		}

		public DateTime LastCleanup
		{
			get;
			private set;
		}

		public bool IsCleanupPastDue
			=> DateTime.Now.Ticks - (_cleanupDelay + 100) * TimeSpan.TicksPerMillisecond > LastCleanup.Ticks;

		public bool IsRunning
		{
			get;
			private set;
		}

		private readonly object _timerSync = new object();
		private Timer? _cleanupTimer;


		protected void ResetTimer()
		{
			Timer? ct2;
			lock (_timerSync)
			{
				ct2 = Interlocked.Exchange(ref _cleanupTimer, null);
			}
			ct2?.Dispose();
		}

		public void SetCleanup(CleanupMode mode = CleanupMode.Deferred)
		{
			if (WasDisposed)
				return;

			switch (mode)
			{
				case CleanupMode.ImmediateSynchronousIfPastDue:
					if (!IsRunning)
						goto case CleanupMode.Deferred;

					if (IsCleanupPastDue)
						goto case CleanupMode.ImmediateSynchronous;

					break;

				case CleanupMode.ImmediateSynchronous:
					Cleanup();
					break;

				case CleanupMode.ImmediateDeferredIfPastDue:
					if (!IsRunning)
						goto case CleanupMode.Deferred;

					if (IsCleanupPastDue)
						goto case CleanupMode.ImmediateDeferred;

					break;

				case CleanupMode.ImmediateDeferred:
					lock (_timerSync)
					{
						if (!WasDisposed && LastCleanup != DateTime.MaxValue)
						{
							// No past due action in order to prevent another thread from firing...
							LastCleanup = DateTime.MaxValue;
							DeferCleanup();
#pragma warning disable CA2008 // Last cleanup value protects against repeat task creation.
							Task.Factory.StartNew(Cleanup);
#pragma warning restore CA2008
						}
					}
					break;

				case CleanupMode.Deferred:
					DeferCleanup();
					break;
			}
		}

		public void DeferCleanup()
		{
			if (WasDisposed) return;
			lock (_timerSync)
			{
				if (WasDisposed) return;
				IsRunning = true;

				if (_cleanupTimer is null)
					_cleanupTimer = new Timer(Cleanup, null, _cleanupDelay, Timeout.Infinite);
				else
					_cleanupTimer.Change(_cleanupDelay, Timeout.Infinite);
			}
		}

		public void ClearCleanup()
		{
			lock (_timerSync)
			{
				IsRunning = false;
				LastCleanup = DateTime.MaxValue;
				//if(_cleanupTimer!=null)
				//_cleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
				ResetTimer();
			}
		}

		private void Cleanup() => Cleanup(null);
		private void Cleanup(object? state)
		{
			if (WasDisposed)
				return; // If another thread enters here after disposal don't allow.

			try
			{
				OnCleanup();
			}
#pragma warning disable CA1031 // Do not catch general exception types
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
#pragma warning restore CA1031 // Do not catch general exception types

			lock (_timerSync)
			{
				LastCleanup = DateTime.Now;
			}

		}

		protected abstract void OnCleanup();

		protected override void OnDispose()
		{
			ResetTimer();
		}

		~DeferredCleanupBase()
		{
			ResetTimer();
		}

	}
}
