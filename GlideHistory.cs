using System.Collections.Generic;

namespace GlideRail
{
    public class GlideHistory
    {
        private readonly Stack<GlideSnapshot> _undo = new();
        private readonly Stack<GlideSnapshot> _redo = new();

        private const int MAX_HISTORY = 64;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public string UndoLabel => CanUndo ? _undo.Peek().Label : "";
        public string RedoLabel => CanRedo ? _redo.Peek().Label : "";

        /// <summary>
        /// Zapisz stan PRZED operacją.
        /// Każda operacja która zmienia KF powinna to wywołać.
        /// </summary>
        public void Push(GlideSnapshot snapshot)
        {
            _undo.Push(snapshot);
            _redo.Clear();  // nowa operacja kasuje redo

            // Ogranicz historię
            if (_undo.Count > MAX_HISTORY)
            {
                var tmp = new Stack<GlideSnapshot>();
                int i = 0;
                foreach (var s in _undo)
                {
                    if (i++ < MAX_HISTORY) tmp.Push(s);
                }
                _undo.Clear();
                foreach (var s in tmp) _undo.Push(s);
            }
        }

        /// <summary>
        /// Cofnij — zwraca snapshot do przywrócenia,
        /// zapisuje aktualny stan na redo.
        /// </summary>
        public GlideSnapshot Undo(GlideSnapshot current)
        {
            if (!CanUndo) return null;
            _redo.Push(current);
            return _undo.Pop();
        }

        /// <summary>
        /// Ponów — zwraca snapshot do przywrócenia,
        /// zapisuje aktualny stan na undo.
        /// </summary>
        public GlideSnapshot Redo(GlideSnapshot current)
        {
            if (!CanRedo) return null;
            _undo.Push(current);
            return _redo.Pop();
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}