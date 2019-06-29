/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Open/blob/dotnet-core/LICENSE.md
 */

using System;
using System.Threading;

namespace Open.Disposable
{
	public class DisposeHandler : DisposableBase
	{
		Action _action;
		public DisposeHandler(Action action)
		{
			_action = action ?? throw new ArgumentNullException(nameof(action));
		}

		protected override void OnDispose(bool calledExplicitly)
			=> Interlocked.Exchange(ref _action, null).Invoke();
		
	}
}
