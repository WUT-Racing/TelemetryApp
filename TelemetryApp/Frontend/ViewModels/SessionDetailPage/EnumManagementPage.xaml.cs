using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using TelemetryApp.Frontend.Data;
using TelemetryApp.Frontend.ViewModels;

namespace TelemetryApp.Frontend.Views
{
    public partial class EnumManagementPage : ContentPage
    {
        private const double RowHeight = 58;
        private const double EdgeSize = 80;

        private EnumStatusReading? _draggedStatus;
        private Grid? _draggedRow;

        private int _firstVisibleIndex;
        private double _dragStartY;
        private double _lastPointerYInViewport;
        private double? _currentPointerYInHost;
        private double _dragPointerOffsetY = RowHeight / 2;

        private int _autoScrollDirection;
        private IDispatcherTimer? _autoScrollTimer;

        public EnumManagementPage(IReadOnlyList<EnumStatusReading> statuses)
        {
            InitializeComponent();
            BindingContext = new EnumManagementViewModel(statuses);
        }

        private void OnStatusCollectionScrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            _firstVisibleIndex = Math.Max(0, e.FirstVisibleItemIndex);

            if (_draggedStatus is null)
            {
                ResetVisibleRowVisualStates();
            }
            else
            {
                ResetVisibleRowVisualStatesExcept(_draggedStatus);
                UpdateDropIndicator();
            }
        }

        private void OnStatusPointerMoved(object sender, PointerEventArgs e)
        {
            var position = e.GetPosition(EnumListHost);

            if (position is null)
                return;

            _currentPointerYInHost = position.Value.Y;

            if (_draggedStatus is null || _draggedRow is null)
                return;

            MovePreviewToPointer(_currentPointerYInHost.Value);
        }

        private void OnStatusPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            var row = GetRowGrid(sender);
            var status = row?.BindingContext as EnumStatusReading;

            if (row is null || status is null)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    StartStatusDrag(row, status);
                    break;

                case GestureStatus.Running:
                    UpdateStatusDrag(e.TotalY);
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    FinishStatusDrag();
                    break;
            }
        }

        private void StartStatusDrag(Grid row, EnumStatusReading status)
        {
            ResetVisibleRowVisualStates();

            _draggedStatus = status;
            _draggedRow = row;

            var rowY = GetRowYInHost(row, status);
            var pointerY = _currentPointerYInHost ?? rowY + RowHeight / 2;

            _dragPointerOffsetY = RowHeight / 2;

            _dragStartY = Math.Clamp(
                pointerY - _dragPointerOffsetY,
                0,
                Math.Max(0, EnumListHost.Height - RowHeight));

            _lastPointerYInViewport = pointerY;

            SetDraggedRowVisualState(row);

            DragPreview.BindingContext = status;
            DragPreview.TranslationY = _dragStartY;
            DragPreview.IsVisible = true;

            DropIndicator.IsVisible = true;
            UpdateDropIndicator();
        }

        private void UpdateStatusDrag(double totalY)
        {
            if (_draggedStatus is null || _draggedRow is null)
                return;

            var pointerY = _currentPointerYInHost ?? _lastPointerYInViewport + totalY;

            MovePreviewToPointer(pointerY);
        }

        private void MovePreviewToPointer(double pointerY)
        {
            var previewY = Math.Clamp(
                pointerY - _dragPointerOffsetY,
                0,
                Math.Max(0, EnumListHost.Height - RowHeight));

            DragPreview.TranslationY = previewY;

            _lastPointerYInViewport = pointerY;

            ResetVisibleRowVisualStatesExcept(_draggedStatus);
            UpdateDropIndicator();
            UpdateAutoScroll();
        }

        private void FinishStatusDrag()
        {
            StopAutoScroll();

            EnumStatusReading? viewportAnchor = null;

            if (BindingContext is EnumManagementViewModel viewModelBefore &&
                viewModelBefore.VisibleStatuses.Count > 0)
            {
                var anchorIndex = Math.Clamp(
                    _firstVisibleIndex,
                    0,
                    viewModelBefore.VisibleStatuses.Count - 1);

                viewportAnchor = viewModelBefore.VisibleStatuses[anchorIndex];
            }

            if (_draggedStatus is not null && BindingContext is EnumManagementViewModel viewModel)
            {
                var targetInsertIndex = CalculateTargetVisibleInsertIndex();
                viewModel.MoveStatusToVisibleIndex(_draggedStatus, targetInsertIndex);
            }

            ResetDragState();
            ResetVisibleRowVisualStates();

            RestoreViewportAnchor(viewportAnchor);
        }

        private int CalculateTargetVisibleInsertIndex()
        {
            if (BindingContext is not EnumManagementViewModel viewModel ||
                viewModel.VisibleStatuses.Count == 0)
            {
                return 0;
            }

            var rowOffset = (int)Math.Round(_lastPointerYInViewport / RowHeight);
            var targetInsertIndex = _firstVisibleIndex + rowOffset;

            return Math.Clamp(targetInsertIndex, 0, viewModel.VisibleStatuses.Count);
        }

        private void UpdateDropIndicator()
        {
            if (BindingContext is not EnumManagementViewModel viewModel ||
                viewModel.VisibleStatuses.Count == 0)
            {
                DropIndicator.IsVisible = false;
                return;
            }

            var targetInsertIndex = CalculateTargetVisibleInsertIndex();

            var y = (targetInsertIndex - _firstVisibleIndex) * RowHeight;

            y = Math.Clamp(
                y,
                0,
                Math.Max(0, EnumListHost.Height - DropIndicator.HeightRequest));

            DropIndicator.TranslationY = y;
            DropIndicator.IsVisible = true;
        }

        private void UpdateAutoScroll()
        {
            if (EnumCollectionView.Height <= 0)
                return;

            if (_lastPointerYInViewport < EdgeSize)
            {
                _autoScrollDirection = -1;
            }
            else if (_lastPointerYInViewport > EnumCollectionView.Height - EdgeSize)
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

                if (BindingContext is not EnumManagementViewModel viewModel ||
                    viewModel.VisibleStatuses.Count == 0)
                {
                    return;
                }

                var nextIndex = Math.Clamp(
                    _firstVisibleIndex + _autoScrollDirection,
                    0,
                    viewModel.VisibleStatuses.Count - 1);

                EnumCollectionView.ScrollTo(
                    nextIndex,
                    position: ScrollToPosition.Start,
                    animate: false);

                ResetVisibleRowVisualStatesExcept(_draggedStatus);
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

        private void RestoreViewportAnchor(EnumStatusReading? viewportAnchor)
        {
            if (viewportAnchor is null)
                return;

            Dispatcher.Dispatch(async () =>
            {
                await Task.Delay(1);

                if (BindingContext is not EnumManagementViewModel viewModel)
                    return;

                if (!viewModel.VisibleStatuses.Contains(viewportAnchor))
                    return;

                EnumCollectionView.ScrollTo(
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

            _draggedStatus = null;
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
            foreach (var row in EnumerateStatusRows(EnumListHost))
            {
                row.Opacity = 1;
                row.ZIndex = 0;
            }
        }

        private void ResetVisibleRowVisualStatesExcept(EnumStatusReading? draggedStatus)
        {
            foreach (var row in EnumerateStatusRows(EnumListHost))
            {
                if (draggedStatus is not null && ReferenceEquals(row.BindingContext, draggedStatus))
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

        private static IEnumerable<Grid> EnumerateStatusRows(Element root)
        {
            if (root is Grid grid && grid.BindingContext is EnumStatusReading)
                yield return grid;

            if (root is not IElementController controller)
                yield break;

            foreach (var child in controller.LogicalChildren)
            {
                foreach (var row in EnumerateStatusRows(child))
                    yield return row;
            }
        }

        private double GetRowYInHost(Grid row, EnumStatusReading status)
        {
            if (TryGetYRelativeTo(row, EnumListHost, out var relativeY))
            {
                return Math.Clamp(
                    relativeY,
                    0,
                    Math.Max(0, EnumListHost.Height - RowHeight));
            }

            var estimatedVisibleIndex = Math.Max(0, status.RowIndex - _firstVisibleIndex);
            var estimatedY = estimatedVisibleIndex * RowHeight;

            return Math.Clamp(
                estimatedY,
                0,
                Math.Max(0, EnumListHost.Height - RowHeight));
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
                if (current is Grid grid && grid.BindingContext is EnumStatusReading)
                    return grid;

                current = current.Parent;
            }

            return null;
        }
    }
}