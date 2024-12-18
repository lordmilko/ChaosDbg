﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ChaosDbg.Text;
using ChaosDbg.Theme;
using ChaosLib;
using FlaUI.Core.AutomationElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dispatcher = System.Windows.Threading.Dispatcher;

namespace ChaosDbg.Tests
{
    class SelectionBuilder
    {
        private Font font;
        private System.Drawing.Point startPoint;
        private double dpi;

        private double yPos;

        private TextCanvas canvas;
        private AutomationElement elm;
        private List<Action> actions = new List<Action>();
        private FakeMouse fakeMouse;

        public SelectionBuilder(App app, TextCanvas canvas, AutomationElement elm)
        {
            dpi = VisualTreeHelper.GetDpi(app.MainWindow).PixelsPerDip;
            startPoint = elm.BoundingRectangle.Location;
            font = App.ServiceProvider.GetService<IThemeProvider>().GetTheme().ContentFont;

            this.canvas = canvas;
            this.elm = elm;
            fakeMouse = new FakeMouse(canvas);
        }

        public SelectionBuilder Click(string skip, string after, int rowOffset = 0)
        {
            actions.Add(() =>
            {
                AddRow(rowOffset);

                var screenPoint = GetDPIAwarePoint(skip + after);
                var clientPoint = GetClientPoint(screenPoint);

                fakeMouse.Click(clientPoint.x, clientPoint.y);
            });

            return this;
        }

        public SelectionBuilder Down(string skip, string after, int rowOffset = 0)
        {
            actions.Add(() =>
            {
                AddRow(rowOffset);

                var screenPoint = GetDPIAwarePoint(skip + after);
                var clientPoint = GetClientPoint(screenPoint);

                fakeMouse.Down(clientPoint.x, clientPoint.y);
            });

            return this;
        }

        public SelectionBuilder Up()
        {
            actions.Add(() =>
            {
                fakeMouse.Up(0, 0);
            });

            return this;
        }

        public SelectionBuilder Move(string skip, string after, int rowOffset = 0)
        {
            actions.Add(() =>
            {
                AddRow(rowOffset);

                var screenPoint = GetDPIAwarePoint(skip + after);
                var clientPoint = GetClientPoint(screenPoint);

                fakeMouse.Move(clientPoint.x, clientPoint.y);
            });

            return this;
        }

        public void Expect(TextPosition start, TextPosition end, string text)
        {
            Expect(e =>
            {
                Assert.AreEqual(start, e.SelectedRange.Start);
                Assert.AreEqual(end, e.SelectedRange.End);
                Assert.AreEqual(text, e.SelectedText);
            });
        }

        public void Expect(params Action<SelectionChangedEventArgs>[] verifiers)
        {
            var hitCount = 0;

            EventHandler<SelectionChangedEventArgs> selectionChanged = (s, e) =>
            {
                if (hitCount >= verifiers.Length)
                    throw new InvalidOperationException($"Hit selection {hitCount + 1} however only expected {verifiers.Length} selections");

                verifiers[hitCount](e);

                hitCount++;
            };

            try
            {
                canvas.MouseManager.SelectionChanged += selectionChanged;

                foreach (var action in actions)
                    action();

                //Wait for all messages to be processed
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            }
            finally
            {
                canvas.MouseManager.SelectionChanged -= selectionChanged;
            }
        }

        private void AddRow(int rowOffset)
        {
            yPos += rowOffset * font.LineHeight;
        }

        private System.Drawing.Point GetDPIAwarePoint(string str)
        {
            var chars = str.ToCharArray();

            double xPos = 0;

            for (var i = 0; i < chars.Length; i++)
            {
                xPos += font.GetCharWidth(chars[i]);
            }

            return new System.Drawing.Point(startPoint.X + 1 + (int) (xPos * dpi), startPoint.Y + 1 + (int) (yPos * dpi));
        }

        private POINT GetClientPoint(System.Drawing.Point screenPoint)
        {
            var clientPoint = new POINT(screenPoint.X, screenPoint.Y);
            var hWnd = ((HwndSource) PresentationSource.FromVisual(canvas)).Handle;
            User32.Native.ScreenToClient(hWnd, ref clientPoint);
            return clientPoint;
        }
    }
}
