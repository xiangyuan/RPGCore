﻿using System;
using System.Diagnostics;

namespace RPGCore.Behaviour
{
	public class EventField<T> : IEventField<T>, IDisposable
	{
		public HandlerCollection Handlers { get; set; }
		public Action OnBeforeChanged;
		public Action OnAfterChanged;

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		private T InternalValue;

		public T Value
		{
			get => InternalValue;
			set
			{
				Handlers.InvokeBeforeChanged ();
				OnBeforeChanged?.Invoke ();

				InternalValue = value;

				Handlers.InvokeAfterChanged ();
				OnAfterChanged?.Invoke ();
			}
		}

		public EventField ()
		{
			Handlers = new HandlerCollection (this);
		}

		public void Dispose ()
		{
			Handlers.Dispose ();
		}
	}
}