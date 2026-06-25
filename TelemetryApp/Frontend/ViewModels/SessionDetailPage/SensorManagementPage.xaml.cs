using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.ViewModels;

namespace TelemetryApp.Frontend.Views
{
    public partial class SensorManagementPage : ContentPage
    {
        private const double RowHeight = 58;
        private const double EdgeSize = 80;

        private SensorReading? _draggedSensor;
        private Grid? _draggedRow;

        private int _firstVisibleIndex;
        private double _dragStartY;
        private double _lastPointerYInViewport;
        private double? _currentPointerYInHost;
        private double _dragPointerOffsetY = RowHeight / 2;

        private int _autoScrollDirection;
        private IDispatcherTimer? _autoScrollTimer;

        public SensorManagementPage(IReadOnlyList<SensorReading> sensors)
        {
            InitializeComponent();
            BindingContext = new SensorManagementViewModel(sensors);
        }

        private void OnSensorCollectionScrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            _firstVisibleIndex = Math.Max(0, e.FirstVisibleItemIndex);

            if (_draggedSensor is null)
            {
                ResetVisibleRowVisualStates();
            }
            else
            {
                ResetVisibleRowVisualStatesExcept(_draggedSensor);
                UpdateDropIndicator();
            }
        }

        private void OnSensorPointerMoved(object sender, PointerEventArgs e)
        {
            var position = e.GetPosition(SensorListHost);

            if (position is null)
                return;

            _currentPointerYInHost = position.Value.Y;

            if (_draggedSensor is null || _draggedRow is null)
                return;

            MovePreviewToPointer(_currentPointerYInHost.Value);
        }

        private void OnSensorPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            var row = GetRowGrid(sender);
            var sensor = row?.BindingContext as SensorReading;

            if (row is null || sensor is null)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    StartSensorDrag(row, sensor);
                    break;

                case GestureStatus.Running:
                    UpdateSensorDrag(e.TotalY);
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    FinishSensorDrag();
                    break;
            }
        }

        private void StartSensorDrag(Grid row, SensorReading sensor)
        {
            ResetVisibleRowVisualStates();

            _draggedSensor = sensor;
            _draggedRow = row;

            var rowY = GetRowYInHost(row, sensor);
            var pointerY = _currentPointerYInHost ?? rowY + RowHeight / 2;

            _dragPointerOffsetY = RowHeight / 2;

            _dragStartY = Math.Clamp(
                pointerY - _dragPointerOffsetY,
                0,
                Math.Max(0, SensorListHost.Height - RowHeight));

            _lastPointerYInViewport = pointerY;

            SetDraggedRowVisualState(row);

            DragPreview.BindingContext = sensor;
            DragPreview.TranslationY = _dragStartY;
            DragPreview.IsVisible = true;

            DropIndicator.IsVisible = true;
            UpdateDropIndicator();
        }

        private void UpdateSensorDrag(double totalY)
        {
            if (_draggedSensor is null || _draggedRow is null)
                return;

            var pointerY = _currentPointerYInHost ?? _lastPointerYInViewport + totalY;

            MovePreviewToPointer(pointerY);
        }

        private void MovePreviewToPointer(double pointerY)
        {
            var previewY = Math.Clamp(
                pointerY - _dragPointerOffsetY,
                0,
                Math.Max(0, SensorListHost.Height - RowHeight));

            DragPreview.TranslationY = previewY;

            _lastPointerYInViewport = pointerY;

            ResetVisibleRowVisualStatesExcept(_draggedSensor);
            UpdateDropIndicator();
            UpdateAutoScroll();
        }

        private void FinishSensorDrag()
        {
            StopAutoScroll();

            SensorReading? viewportAnchor = null;

            if (BindingContext is SensorManagementViewModel viewModelBefore &&
                viewModelBefore.VisibleSensors.Count > 0)
            {
                var anchorIndex = Math.Clamp(
                    _firstVisibleIndex,
                    0,
                    viewModelBefore.VisibleSensors.Count - 1);

                viewportAnchor = viewModelBefore.VisibleSensors[anchorIndex];
            }

            if (_draggedSensor is not null && BindingContext is SensorManagementViewModel viewModel)
            {
                var targetInsertIndex = CalculateTargetVisibleInsertIndex();
                viewModel.MoveSensorToVisibleIndex(_draggedSensor, targetInsertIndex);
            }

            ResetDragState();
            ResetVisibleRowVisualStates();

            RestoreViewportAnchor(viewportAnchor);
        }

        private int CalculateTargetVisibleInsertIndex()
        {
            if (BindingContext is not SensorManagementViewModel viewModel ||
                viewModel.VisibleSensors.Count == 0)
            {
                return 0;
            }

            var rowOffset = (int)Math.Round(_lastPointerYInViewport / RowHeight);
            var targetInsertIndex = _firstVisibleIndex + rowOffset;

            return Math.Clamp(targetInsertIndex, 0, viewModel.VisibleSensors.Count);
        }

        private void UpdateDropIndicator()
        {
            if (BindingContext is not SensorManagementViewModel viewModel ||
                viewModel.VisibleSensors.Count == 0)
            {
                DropIndicator.IsVisible = false;
                return;
            }

            var targetInsertIndex = CalculateTargetVisibleInsertIndex();

            var y = (targetInsertIndex - _firstVisibleIndex) * RowHeight;

            y = Math.Clamp(
                y,
                0,
                Math.Max(0, SensorListHost.Height - DropIndicator.HeightRequest));

            DropIndicator.TranslationY = y;
            DropIndicator.IsVisible = true;
        }

        private void UpdateAutoScroll()
        {
            if (SensorCollectionView.Height <= 0)
                return;

            if (_lastPointerYInViewport < EdgeSize)
            {
                _autoScrollDirection = -1;
            }
            else if (_lastPointerYInViewport > SensorCollectionView.Height - EdgeSize)
            {
                _autoScrollDirection = 1;
            }
            else
            {
                _autoScrollDirection = 0;
            }

            EnsureAutoScrollTimer();
        }

        private void EnsureAutoScrollTimer()
        {
            if (_autoScrollTimer is not null)
                return;

            _autoScrollTimer = Dispatcher.CreateTimer();
            _autoScrollTimer.Interval = TimeSpan.FromMilliseconds(80);
            _autoScrollTimer.Tick += (_, _) =>
            {
                if (_autoScrollDirection == 0)
                    return;

                if (BindingContext is not SensorManagementViewModel viewModel ||
                    viewModel.VisibleSensors.Count == 0)
                {
                    return;
                }

                var nextIndex = Math.Clamp(
                    _firstVisibleIndex + _autoScrollDirection,
                    0,
                    viewModel.VisibleSensors.Count - 1);

                SensorCollectionView.ScrollTo(
                    nextIndex,
                    position: ScrollToPosition.Start,
                    animate: false);

                ResetVisibleRowVisualStatesExcept(_draggedSensor);
                UpdateDropIndicator();
            };

            _autoScrollTimer.Start();
        }

        private void StopAutoScroll()
        {
            _autoScrollDirection = 0;

            if (_autoScrollTimer is null)
                return;

            _autoScrollTimer.Stop();
            _autoScrollTimer = null;
        }

        private void RestoreViewportAnchor(SensorReading? viewportAnchor)
        {
            if (viewportAnchor is null)
                return;

            Dispatcher.Dispatch(async () =>
            {
                await Task.Delay(1);

                if (BindingContext is not SensorManagementViewModel viewModel)
                    return;

                if (!viewModel.VisibleSensors.Contains(viewportAnchor))
                    return;

                SensorCollectionView.ScrollTo(
                    viewportAnchor,
                    position: ScrollToPosition.Start,
                    animate: false);

                ResetVisibleRowVisualStates();
            });
        }

        private void ResetDragState()
        {
            DragPreview.IsVisible = false;
            DragPreview.BindingContext = null;
            DragPreview.TranslationY = 0;

            DropIndicator.IsVisible = false;
            DropIndicator.TranslationY = 0;

            _draggedSensor = null;
            _draggedRow = null;
            _dragStartY = 0;
            _lastPointerYInViewport = 0;
            _currentPointerYInHost = null;
            _dragPointerOffsetY = RowHeight / 2;
        }

        private void SetDraggedRowVisualState(Grid row)
        {
            row.Opacity = 0.18;
            row.ZIndex = 0;
        }

        private void ResetVisibleRowVisualStates()
        {
            foreach (var row in EnumerateSensorRows(SensorListHost))
            {
                row.Opacity = 1;
                row.ZIndex = 0;
            }
        }

        private void ResetVisibleRowVisualStatesExcept(SensorReading? draggedSensor)
        {
            foreach (var row in EnumerateSensorRows(SensorListHost))
            {
                if (draggedSensor is not null && ReferenceEquals(row.BindingContext, draggedSensor))
                {
                    SetDraggedRowVisualState(row);
                }
                else
                {
                    row.Opacity = 1;
                    row.ZIndex = 0;
                }
            }
        }

        private static IEnumerable<Grid> EnumerateSensorRows(Element root)
        {
            if (root is Grid grid && grid.BindingContext is SensorReading)
                yield return grid;

            if (root is not IElementController controller)
                yield break;

            foreach (var child in controller.LogicalChildren)
            {
                foreach (var row in EnumerateSensorRows(child))
                    yield return row;
            }
        }

        private double GetRowYInHost(Grid row, SensorReading sensor)
        {
            if (TryGetYRelativeTo(row, SensorListHost, out var relativeY))
            {
                return Math.Clamp(
                    relativeY,
                    0,
                    Math.Max(0, SensorListHost.Height - RowHeight));
            }

            var estimatedVisibleIndex = Math.Max(0, sensor.RowIndex - _firstVisibleIndex);
            var estimatedY = estimatedVisibleIndex * RowHeight;

            return Math.Clamp(
                estimatedY,
                0,
                Math.Max(0, SensorListHost.Height - RowHeight));
        }

        private static bool TryGetYRelativeTo(VisualElement element, Element ancestor, out double y)
        {
            y = 0;

            Element? current = element;

            while (current is not null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;

                if (current is VisualElement visual)
                    y += visual.Y + visual.TranslationY;

                current = current.Parent;
            }

            return false;
        }

        private static Grid? GetRowGrid(object sender)
        {
            if (sender is not Element element)
                return null;

            var current = element;

            while (current is not null)
            {
                if (current is Grid grid && grid.BindingContext is SensorReading)
                    return grid;

                current = current.Parent;
            }

            return null;
        }
    }
}