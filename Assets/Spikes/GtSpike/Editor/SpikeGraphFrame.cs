using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gorecode.Bt.Spikes.GtSpike
{
    // Throwaway P1 helper for the screenshot pass: fits the content of every open
    // graph window to its viewport and logs what the window rendered. Kept as P1
    // evidence (docs/gt-notes.md, "Opening the graph window").
    public static class SpikeGraphFrame
    {
        const string k_CanvasTypeName = "GraphViewImp";
        const string k_NodeClass = "ge-node";
        const string k_PendingText = "Graph Processing Pending";

        enum PendingLabel { NotFound, Hidden, Shown }

        [MenuItem("spikes/Fit graph to window")]
        public static void FitAll()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (!(window is Unity.GraphToolkit.Editor.IGraphWindow))
                {
                    continue;
                }

                VisualElement canvas = null;
                var nodeCount = 0;
                var pending = PendingLabel.NotFound;
                Inspect(window.rootVisualElement, ref canvas, ref nodeCount, ref pending);

                Debug.Log("[GtSpike] window '" + window.titleContent.text + "': canvas "
                    + (canvas != null ? "found" : "missing") + ", node views " + nodeCount
                    + ", pending label " + pending);

                if (canvas == null || nodeCount == 0)
                {
                    continue;
                }

                // The canvas binds frame-all to the A key and exposes no public method for it.
                var systemEvent = new Event { type = EventType.KeyDown, keyCode = KeyCode.A, character = 'a' };
                using (var keyDown = KeyDownEvent.GetPooled(systemEvent))
                {
                    keyDown.target = canvas;
                    canvas.SendEvent(keyDown);
                }

                window.Focus();
                window.Repaint();
            }
        }

        // One pass over the window's visual tree: the canvas, the node views and the
        // state of the "pending" label.
        static void Inspect(VisualElement element, ref VisualElement canvas, ref int nodeCount, ref PendingLabel pending)
        {
            if (canvas == null && element.GetType().Name == k_CanvasTypeName)
            {
                canvas = element;
            }

            if (element.ClassListContains(k_NodeClass))
            {
                nodeCount++;
            }

            if (pending != PendingLabel.Shown && element is Label label && label.text == k_PendingText)
            {
                // The label stays in the tree once processing is done; it is only hidden.
                pending = label.resolvedStyle.display != DisplayStyle.None ? PendingLabel.Shown : PendingLabel.Hidden;
            }

            for (var i = 0; i < element.childCount; i++)
            {
                Inspect(element[i], ref canvas, ref nodeCount, ref pending);
            }
        }
    }
}
