using System.Collections.Generic;

public sealed class CancelStack
{
    private readonly List<ICancelable> cancelables = new List<ICancelable>();

    public void Register(ICancelable cancelable)
    {
        if (cancelable == null)
            return;

        cancelables.Remove(cancelable);
        cancelables.Add(cancelable);
    }

    public void Unregister(ICancelable cancelable)
    {
        if (cancelable == null)
            return;

        cancelables.Remove(cancelable);
    }

    public bool TryCancel()
    {
        for (int index = cancelables.Count - 1; index >= 0; index--)
        {
            if (cancelables[index]?.TryCancel() == true)
                return true;
        }

        return false;
    }
}
