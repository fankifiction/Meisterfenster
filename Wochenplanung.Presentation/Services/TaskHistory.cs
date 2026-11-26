namespace Wochenplanung.Presentation.Services;

public sealed class TaskHistory
{
    private readonly Stack<HistoryAction> _undo = new();
    private readonly Stack<HistoryAction> _redo = new();
    private bool _isApplying;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsApplying => _isApplying;

    public void Push(Func<Task> undoAsync, Func<Task> redoAsync)
    {
        if (_isApplying)
            return;

        _undo.Push(new HistoryAction(undoAsync, redoAsync));
        _redo.Clear();
    }

    public async Task UndoAsync()
    {
        if (_undo.Count == 0)
            return;

        var action = _undo.Pop();
        _isApplying = true;
        try
        {
            await action.UndoAsync();
            _redo.Push(action);
        }
        finally
        {
            _isApplying = false;
        }
    }

    public async Task RedoAsync()
    {
        if (_redo.Count == 0)
            return;

        var action = _redo.Pop();
        _isApplying = true;
        try
        {
            await action.RedoAsync();
            _undo.Push(action);
        }
        finally
        {
            _isApplying = false;
        }
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private sealed record HistoryAction(Func<Task> UndoAsync, Func<Task> RedoAsync);
}
