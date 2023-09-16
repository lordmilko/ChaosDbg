using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ChaosDbg.Tests.LayoutFactory;

namespace ChaosDbg.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class SplitPaneTests
    {
        [TestMethod]
        public void SplitPane_TwoColumns()
        {
            var xaml = XamlFactory.SplitterItemsDockContainer(
                Orientation.Horizontal,
                XamlFactory.SplitterItemsDockContainer(),
                XamlFactory.SplitterItemsDockContainer()
            );

            var expected = Expected<SplitterItemsDockContainerInfo>(width: 785.6, height: 412.8, v => v.Verify(dockedWidth: 100, dockedHeight: 100, orientation: Orientation.Horizontal),
                Inherited<SplitterItemsControlInfo>(
                    Inherited<SplitterPanelInfo>(
                        Expected<SplitterItemInfo>(width: 392.8, height: 412.8,
                            Inherited<SplitterItemsDockContainerInfo>(
                                Explicit<SplitterGripInfo>(x: 388, y: 0, width: 5, height: 412.8),
                                Explicit<SplitterItemsControlInfo>(x: 0, y: 0, width: 387.8, height: 412.8,
                                    Inherited<SplitterPanelInfo>()
                                )
                            )
                        ),
                        Expected<SplitterItemInfo>(width: 392.8, height: 412.8,
                            Inherited<SplitterItemsDockContainerInfo>(
                                Expected<SplitterGripInfo>(0, 0),
                                Expected<SplitterItemsControlInfo>(width: 392.8, height: 412.8,
                                    Inherited<SplitterPanelInfo>()
                                )
                            )
                        )
                    )
                )
            );

            Test(xaml, expected);
        }

        [TestMethod]
        public void SplitPane_TwoRows()
        {
            var xaml = XamlFactory.SplitterItemsDockContainer(
                Orientation.Vertical,
                XamlFactory.SplitterItemsDockContainer(),
                XamlFactory.SplitterItemsDockContainer()
            );

            var expected = Expected<SplitterItemsDockContainerInfo>(width: 785.6, height: 412.8,
                Inherited<SplitterItemsControlInfo>(
                    Inherited<SplitterPanelInfo>(
                        Expected<SplitterItemInfo>(width: 785.6, height: 206.4,
                            Inherited<SplitterItemsDockContainerInfo>(
                                Explicit<SplitterGripInfo>(x: 0, y: 201.6, width: 785.6, height: 5),
                                Explicit<SplitterItemsControlInfo>(x: 0, y: 0, width: 785.6, height: 201.4,
                                    Inherited<SplitterPanelInfo>()
                                )
                            )
                        ),
                        Expected<SplitterItemInfo>(width: 785.6, height: 206.4,
                            Inherited<SplitterItemsDockContainerInfo>(
                                Expected<SplitterGripInfo>(width: 0, height: 0),
                                Expected<SplitterItemsControlInfo>(width: 785.6, height: 206.4,
                                    Inherited<SplitterPanelInfo>()
                                )
                            )
                        )
                    )
                )
            );

            Test(xaml, expected);
        }

        [TestMethod]
        public void SplitPane_Complex()
        {
            var xaml = XamlFactory.SplitterItemsDockContainer(
                Orientation.Horizontal,
                XamlFactory.SplitterItemsDockContainer(
                    Orientation.Vertical,
                    dockedWidth: "350",
                    children: new[]
                    {
                        XamlFactory.SplitterItemsDockContainer(dockedHeight: "240"),
                        XamlFactory.SplitterItemsDockContainer(
                            Orientation.Horizontal,
                            XamlFactory.SplitterItemsDockContainer(),
                            XamlFactory.SplitterItemsDockContainer()
                        )
                    }
                ),
                XamlFactory.SplitterItemsDockContainer(
                    Orientation.Vertical,
                    XamlFactory.SplitterItemsDockContainer(),
                    XamlFactory.SplitterItemsDockContainer(),
                    XamlFactory.SplitterItemsDockContainer()
                )
            );

            var expected = Expected<SplitterItemsDockContainerInfo>(width: 785.6, height: 412.8,
                Inherited<SplitterItemsControlInfo>(
                    Inherited<SplitterPanelInfo>(
                        //Item
                        Expected<SplitterItemInfo>(width: 611.022222222222, height: 412.8,
                            Expected<SplitterItemsDockContainerInfo>(width: 611.022222222222, height: 412.8,
                                Explicit<SplitterGripInfo>(x: 606.4, y: 0, width: 5, height: 412.8),
                                Explicit<SplitterItemsControlInfo>(x: 0, y: 0, width: 606.022222222222, height: 412.8,
                                    Expected<SplitterPanelInfo>(width: 606.022222222222, height: 412.8,
                                        //Item
                                        Expected<SplitterItemInfo>(width: 606.022222222222, height: 291.388235294118,
                                            Expected<SplitterItemsDockContainerInfo>(width: 606.022222222222, height: 291.388235294118,
                                                Explicit<SplitterGripInfo>(x: 0, y: 286.4, width: 606.022222222222, height: 5),
                                                Explicit<SplitterItemsControlInfo>(x: 0, y: 0, width: 606.022222222222, height: 286.388235294118,
                                                    Expected<SplitterPanelInfo>(width: 606.022222222222, height: 286.388235294118)
                                                )
                                            )
                                        ),

                                        //Item
                                        Explicit<SplitterItemInfo>(x: 0, y: 291.2, width: 606.022222222222, height: 121.411764705882,
                                            Expected<SplitterItemsDockContainerInfo>(width: 606.022222222222, height: 121.411764705882,
                                                Explicit<SplitterGripInfo>(x: 0, y: 291.2, width: 0, height: 0),
                                                Explicit<SplitterItemsControlInfo>(x: 0, y: 291.2, width: 606.022222222222, height: 121.411764705882,
                                                    Expected<SplitterPanelInfo>(width: 606.022222222222, height: 121.411764705882,
                                                        Expected<SplitterItemInfo>(width: 303.011111111111, height: 121.411764705882,
                                                            Expected<SplitterItemsDockContainerInfo>(width: 303.011111111111, height: 121.411764705882,
                                                                Explicit<SplitterGripInfo>(x: 298.4, y: 291.2, width: 5, height: 121.411764705882),
                                                                Explicit<SplitterItemsControlInfo>(x: 0, y: 291.2, width: 298.011111111111, height: 121.411764705882,
                                                                    Expected<SplitterPanelInfo>(width: 298.011111111111, height: 121.411764705882)
                                                                )
                                                            )
                                                        ),
                                                        Explicit<SplitterItemInfo>(x: 303.2, y: 291.2, width: 303.011111111111, height: 121.411764705882,
                                                            Expected<SplitterItemsDockContainerInfo>(width: 303.011111111111, height: 121.411764705882,
                                                                Explicit<SplitterGripInfo>(x: 303.2, y: 291.2, width: 0, height: 0),
                                                                Explicit<SplitterItemsControlInfo>(x: 303.2, y: 291.2, width: 303.011111111111, height: 121.411764705882,
                                                                    Expected<SplitterPanelInfo>(width: 303.011111111111, height: 121.411764705882)
                                                                )
                                                            )
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        //Item
                        Explicit<SplitterItemInfo>(x: 611.2, y: 0, width: 174.577777777778, height: 412.8,
                            Expected<SplitterItemsDockContainerInfo>(width: 174.577777777778, height: 412.8,
                                Explicit<SplitterGripInfo>(x: 611.2, y: 0, width: 0, height: 0),
                                Explicit<SplitterItemsControlInfo>(x: 611.2, y: 0, width: 174.577777777778, height: 412.8,
                                    Expected<SplitterPanelInfo>(width: 174.577777777778, height: 412.8,
                                        //Item
                                        Expected<SplitterItemInfo>(width: 174.577777777778, height: 137.6,
                                            Expected<SplitterItemsDockContainerInfo>(width: 174.577777777778, height: 137.6,
                                                Explicit<SplitterGripInfo>(x: 611.2, y: 132.8, width: 174.577777777778, height: 5),
                                                Explicit<SplitterItemsControlInfo>(x: 611.2, y: 0, width: 174.577777777778, height: 132.6,
                                                    Expected<SplitterPanelInfo>(width: 174.577777777778, height: 132.6)
                                                )
                                            )
                                        ),
                                        //Item
                                        Expected<SplitterItemInfo>(width: 174.577777777778, height: 137.6,
                                            Expected<SplitterItemsDockContainerInfo>(width: 174.577777777778, height: 137.6,
                                                Explicit<SplitterGripInfo>(x: 611.2, y: 270.4, width: 174.577777777778, height: 5),
                                                Explicit<SplitterItemsControlInfo>(x: 611.2, y: 137.6, width: 174.577777777778, height: 132.6,
                                                    Expected<SplitterPanelInfo>(width: 174.577777777778, height: 132.6)
                                                )
                                            )
                                        ),
                                        //Item
                                        Expected<SplitterItemInfo>(width: 174.577777777778, height: 137.6,
                                            Expected<SplitterItemsDockContainerInfo>(width: 174.577777777778, height: 137.6,
                                                Explicit<SplitterGripInfo>(x: 611.2, y: 275.2, width: 0, height: 0),
                                                Explicit<SplitterItemsControlInfo>(x: 611.2, y: 275.2, width: 174.577777777778, height: 137.6,
                                                    Expected<SplitterPanelInfo>(width: 174.577777777778, height: 137.6)
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );

            Test(xaml, expected);
        }

        private void Test(XamlElement xaml, ControlInfo expected)
        {
            AppRunner.WithCustomXaml(xaml, w =>
            {
                var visitor = new PaneVisualTreeVisitor();

                var root = visitor.Visit(w);

                CompareTree(expected, root);
            });
        }

        private void CompareTree(ControlInfo expected, IPaneItem actual)
        {
            var expectedPath = GetAncestorExpectedPath(expected);

            Assert.AreEqual(expected.Type, actual.GetType(), $"Expected type was not correct. Path:{Environment.NewLine}{expectedPath}");
            Assert.IsTrue(
                RectEqualityComparer.Instance.Equals(expected.Bounds, actual.Bounds.Normal),
                $"Expected:<{expected.Bounds}>. Actual:<{actual.Bounds.Normal}>. Expected bounds were not correct. Path:{Environment.NewLine}{expectedPath}"
            );

            expected.Verify?.Invoke(actual);

            if (expected.Children.Length != actual.Children.Length)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Expected children: {expected.Children.Length}. Actual: {actual.Children.Length}");
                builder.AppendLine().AppendLine();
                builder.AppendLine("Parent:");
                builder.AppendLine(expectedPath);

                var children = actual.Children.Select(GetDescendantActualPath).ToArray();

                builder.AppendLine("Actual children:");
                builder.Append(string.Join(Environment.NewLine, children));

                throw new InvalidOperationException(builder.ToString());
            }

            for (var i = 0; i < actual.Children.Length; i++)
                CompareTree(expected.Children[i], actual.Children[i]);
        }

        private string GetAncestorExpectedPath(ControlInfo expected)
        {
            var ancestors = new List<ControlInfo>();

            var parent = expected;

            while (parent != null)
            {
                ancestors.Add(parent);

                parent = parent.Parent;
            }

            ancestors.Reverse();

            var lines = new List<string>();

            for (var i = 0; i < ancestors.Count; i++)
            {
                var indent = new string(' ', Math.Max(0, i - 1) * 4);

                if (i == 0)
                    lines.Add(indent + ancestors[i]);
                else
                {
                    var current = ancestors[i];
                    var myParent = current.Parent;

                    var childIndex = Array.IndexOf(myParent.Children, current);

                    for (var j = 0; j < childIndex; j++)
                        lines.Add(indent + "    ...");

                    lines.Add(indent + "    " + (i == ancestors.Count - 1 ? "==> " : string.Empty) + current);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string GetDescendantActualPath(IPaneItem actual)
        {
            var lines = new List<string>();

            int depth = 0;

            void WriteChildren(IPaneItem parent)
            {
                var indent = new string(' ', depth * 4);

                //lines.Add(indent + $"[{parent.GetType().Name}] Width = {parent.Bounds.Normal.Width}, Height = {parent.Bounds.Normal.Height}");
                lines.Add(indent + $"Expected<{parent.GetType().Name}>(width: {parent.Bounds.Normal.Width}, height: {parent.Bounds.Normal.Height}{(parent.Children.Length == 0 ? ")," : ",")}");

                depth++;

                foreach (var child in parent.Children)
                    WriteChildren(child);

                depth--;

                if (parent.Children.Length > 0)
                    lines.Add(indent + ")");
            }

            WriteChildren(actual);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
