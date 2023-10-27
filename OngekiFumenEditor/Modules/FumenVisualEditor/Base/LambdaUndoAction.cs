using Gemini.Modules.UndoRedo;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
	public class LambdaUndoAction : IUndoableAction
	{
		private string actionName;
		private Action executeOrRedo;
		private Action undo;

		public LambdaUndoAction(string actionName, Action executeOrRedo, Action undo)
		{
			this.actionName = actionName;
			this.executeOrRedo = executeOrRedo;
			this.undo = undo;
		}

		public string Name => actionName;
		public void Execute() => executeOrRedo();
		public void Undo() => undo();

		public static LambdaUndoAction Create(string actionName, Action executeOrRedo, Action undo) => new LambdaUndoAction(actionName, executeOrRedo, undo);
	}
}
