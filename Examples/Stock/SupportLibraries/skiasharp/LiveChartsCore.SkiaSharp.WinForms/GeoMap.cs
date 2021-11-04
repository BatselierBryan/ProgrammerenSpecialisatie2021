﻿// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using LiveChartsCore.Drawing;
using LiveChartsCore.Geo;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace LiveChartsCore.SkiaSharpView.WinForms
{
    /// <summary>
    /// The geo map control.
    /// </summary>
    /// <seealso cref="UserControl" />
    public partial class GeoMap : UserControl, IGeoMapView<SkiaSharpDrawingContext>
    {
        private readonly CollectionDeepObserver<IMapElement> _shapesObserver;
        private readonly GeoMap<SkiaSharpDrawingContext> _core;
        private IEnumerable<IMapElement> _shapes = Enumerable.Empty<IMapElement>();
        private CoreMap<SkiaSharpDrawingContext> _activeMap;
        private MapProjection _mapProjection = MapProjection.Default;
        private LvcColor[] _heatMap = new[]
        {
            LvcColor.FromArgb(255, 179, 229, 252), // cold (min value)
            LvcColor.FromArgb(255, 2, 136, 209) // hot (max value)
        };
        private double[]? _colorStops = null;
        private IPaint<SkiaSharpDrawingContext>? _stroke = new SolidColorPaint(new SKColor(255, 255, 255, 255)) { IsStroke = true };
        private IPaint<SkiaSharpDrawingContext>? _fill = new SolidColorPaint(new SKColor(240, 240, 240, 255)) { IsFill = true };
        private object? _viewCommand = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeoMap"/> class.
        /// </summary>
        public GeoMap()
        {
            InitializeComponent();
            if (!LiveCharts.IsConfigured) LiveCharts.Configure(LiveChartsSkiaSharp.DefaultPlatformBuilder);
            _activeMap = Maps.GetWorldMap<SkiaSharpDrawingContext>();

            _core = new GeoMap<SkiaSharpDrawingContext>(this);
            _shapesObserver = new CollectionDeepObserver<IMapElement>(
                (object? sender, NotifyCollectionChangedEventArgs e) => _core?.Update(),
                (object? sender, PropertyChangedEventArgs e) => _core?.Update(),
                true);

            var c = Controls[0].Controls[0];

            c.MouseDown += OnMouseDown;
            c.MouseMove += OnMouseMove;
            c.MouseUp += OnMouseUp;
            c.MouseLeave += OnMouseLeave;
            c.MouseWheel += OnMouseWheel;

            Resize += GeoMap_Resize;
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Canvas"/>
        public MotionCanvas<SkiaSharpDrawingContext> Canvas => motionCanvas1.CanvasCore;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.AutoUpdateEnabled" />
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool AutoUpdateEnabled { get; set; } = true;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.DesignerMode" />
        bool IGeoMapView<SkiaSharpDrawingContext>.DesignerMode => LicenseManager.UsageMode == LicenseUsageMode.Designtime;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.SyncContext" />
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object SyncContext { get; set; } = new object();

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.ViewCommand" />
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? ViewCommand
        {
            get => _viewCommand;
            set
            {
                _viewCommand = value;
                if (value is not null) _core.ViewTo(value);
            }
        }
        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.ActiveMap"/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CoreMap<SkiaSharpDrawingContext> ActiveMap { get => _activeMap; set { _activeMap = value; OnPropertyChanged(); } }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Width"/>
        float IGeoMapView<SkiaSharpDrawingContext>.Width => ClientSize.Width;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Height"/>
        float IGeoMapView<SkiaSharpDrawingContext>.Height => ClientSize.Height;

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.MapProjection"/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MapProjection MapProjection { get => _mapProjection; set { _mapProjection = value; OnPropertyChanged(); } }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.HeatMap"/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public LvcColor[] HeatMap { get => _heatMap; set { _heatMap = value; OnPropertyChanged(); } }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.ColorStops"/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double[]? ColorStops { get => _colorStops; set { _colorStops = value; OnPropertyChanged(); } }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Stroke"/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IPaint<SkiaSharpDrawingContext>? Stroke
        {
            get => _stroke;
            set
            {
                if (value is not null) value.IsStroke = true;
                _stroke = value;
                OnPropertyChanged();
            }
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Fill"/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IPaint<SkiaSharpDrawingContext>? Fill
        {
            get => _fill;
            set
            {
                if (value is not null) value.IsFill = true;
                _fill = value;
                OnPropertyChanged();
            }
        }

        /// <inheritdoc cref="IGeoMapView{TDrawingContext}.Shapes"/>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<IMapElement> Shapes
        {
            get => _shapes;
            set
            {
                _shapesObserver.Dispose(_shapes);
                _shapesObserver.Initialize(value);
                _shapes = value;
                OnPropertyChanged();
            }
        }

        void IGeoMapView<SkiaSharpDrawingContext>.InvokeOnUIThread(Action action)
        {
            if (!IsHandleCreated) return;
            _ = BeginInvoke(action).AsyncWaitHandle.WaitOne();
        }

        /// <summary>
        /// Called when a property changes.
        /// </summary>
        protected void OnPropertyChanged()
        {
            _core?.Update();
        }

        private void GeoMap_Resize(object? sender, EventArgs e)
        {
            _core?.Update();
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            _core?.InvokePointerDown(new LvcPoint(e.Location.X, e.Location.Y));
        }
        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            _core?.InvokePointerMove(new LvcPoint(e.Location.X, e.Location.Y));
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            _core?.InvokePointerUp(new LvcPoint(e.Location.X, e.Location.Y));
        }

        private void OnMouseLeave(object? sender, EventArgs e)
        {
            _core?.InvokePointerLeft();
        }

        private void OnMouseWheel(object? sender, MouseEventArgs e)
        {
            if (_core is null) throw new Exception("core not found");
            var p = e.Location;
            _core.ViewTo(
                new ZoomOnPointerView(new LvcPoint(p.X, p.Y), e.Delta > 0 ? ZoomDirection.ZoomIn : ZoomDirection.ZoomOut));
            Capture = true;
        }
    }
}
