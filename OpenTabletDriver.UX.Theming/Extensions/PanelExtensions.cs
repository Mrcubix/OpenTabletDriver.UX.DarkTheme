using System.Collections.Generic;
using Eto.Forms;

namespace OpenTabletDriver.UX.Theming.Extensions;

public static class ContainerExtensions
{
    public static IEnumerable<T> GetChildren<T>(this Container panel) where T : Control
    {
        // recursively get all children
        foreach (var child in panel.Controls)
        {
            if (child is T t)
                yield return t;

            if (child is Container c)
                foreach (var result in c.GetChildren<T>())
                    yield return result;
        }
    }
}