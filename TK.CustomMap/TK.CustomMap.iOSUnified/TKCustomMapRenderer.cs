﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CoreGraphics;
using Foundation;
using MapKit;
using TK.CustomMap;
using TK.CustomMap.iOSUnified;
using TK.CustomMap.Overlays;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Maps.iOS;
using Xamarin.Forms.Platform.iOS;

[assembly: ExportRenderer(typeof(TKCustomMap), typeof(TKCustomMapRenderer))]

namespace TK.CustomMap.iOSUnified
{
    /// <summary>
    /// iOS Renderer of <see cref="TK.CustomMap.TKCustomMap"/>
    /// </summary>
    public class TKCustomMapRenderer : MapRenderer
    {
        private const string AnnotationIdentifier = "TKCustomAnnotation";
        private const string AnnotationIdentifierDefaultPin = "TKCustomAnnotationDefaultPin";

        private readonly Dictionary<MKPolyline, TKRoute> _routes = new Dictionary<MKPolyline, TKRoute>();
        private readonly Dictionary<MKCircle, TKCircle> _circles = new Dictionary<MKCircle, TKCircle>();
        private readonly Dictionary<MKPolygon, TKPolygon> _polygons = new Dictionary<MKPolygon, TKPolygon>();

        private bool _firstUpdate = true;
        private bool _isDragging;
        private IMKAnnotation _selectedAnnotation;

        private MKMapView Map
        {
            get { return this.Control as MKMapView; }
        }
        private TKCustomMap FormsMap
        {
            get { return this.Element as TKCustomMap; }
        }
        /// <summary>
        /// Dummy function to avoid linker.
        /// </summary>
        [Preserve]
        public static void InitMapRenderer()
        { }
        /// <inheritdoc/>
        protected override void OnElementChanged(ElementChangedEventArgs<View> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement != null || this.FormsMap == null || this.Map == null) return;

            this.Map.GetViewForAnnotation = this.GetViewForAnnotation;
            this.Map.OverlayRenderer = this.GetOverlayRenderer; 
            this.Map.DidSelectAnnotationView += OnDidSelectAnnotationView;
            this.Map.RegionChanged += OnMapRegionChanged;
            this.Map.ChangedDragState += OnChangedDragState;
            this.Map.CalloutAccessoryControlTapped += OnMapCalloutAccessoryControlTapped;

            this.Map.AddGestureRecognizer(new UILongPressGestureRecognizer(this.OnMapLongPress));
            this.Map.AddGestureRecognizer(new UITapGestureRecognizer(this.OnMapClicked));

            if (this.FormsMap.CustomPins != null)
            {
                this.UpdatePins();
                this.FormsMap.CustomPins.CollectionChanged += OnCollectionChanged;
            }
            this.SetMapCenter();
            this.UpdateRoutes();
            this.UpdateCircles();
            this.UpdatePolygons();
            this.FormsMap.PropertyChanged += OnMapPropertyChanged;
        }

        /// <summary>
        /// Get the overlay renderer
        /// </summary>
        /// <param name="mapView">The <see cref="MKMapView"/></param>
        /// <param name="overlay">The overlay to render</param>
        /// <returns>The overlay renderer</returns>
        private MKOverlayRenderer GetOverlayRenderer(MKMapView mapView, IMKOverlay overlay)
        {
            var polyline = overlay as MKPolyline;
            if (polyline != null)
            {
                var route = this._routes[polyline];

                return new MKPolylineRenderer(polyline) 
                {
                    FillColor = route.LineColor.ToUIColor(),
                    LineWidth = route.LineWidth,
                    StrokeColor = route.LineColor.ToUIColor(),
                };
            }

            var mkCircle = overlay as MKCircle;
            if (mkCircle != null)
            {
                var circle = this._circles[mkCircle];

                return new MKCircleRenderer(mkCircle)
                {
                    FillColor = circle.Color.ToUIColor(),
                    StrokeColor = circle.StrokeColor.ToUIColor(),
                    LineWidth = circle.StrokeWidth
                };
            }

            var mkPolygon = overlay as MKPolygon;
            if (mkPolygon != null)
            {
                var polygon = this._polygons[mkPolygon];

                return new MKPolygonRenderer(mkPolygon) 
                {
                    FillColor = polygon.FillColor.ToUIColor(),
                    StrokeColor = polygon.StrokeColor.ToUIColor(),
                    LineWidth = polygon.StrokeWidth
                };
            }
            return null;
        }
        /// <summary>
        /// When a property of the forms map changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMapPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == TKCustomMap.CustomPinsProperty.PropertyName)
            {
                this._firstUpdate = true;
                this.UpdatePins();
            }
            else if (e.PropertyName == TKCustomMap.SelectedPinProperty.PropertyName)
            {
                this.SetSelectedPin();
            }
            else if (e.PropertyName == TKCustomMap.MapCenterProperty.PropertyName)
            {
                this.SetMapCenter();
            }
            else if (e.PropertyName == TKCustomMap.RoutesProperty.PropertyName)
            {
                this.UpdateRoutes();
            }
            else if (e.PropertyName == TKCustomMap.CalloutClickedCommandProperty.PropertyName)
            {
                this.UpdatePins();
            }
            else if(e.PropertyName == TKCustomMap.PolygonsProperty.PropertyName)
            {
                this.UpdatePolygons();
            }
        }
        /// <summary>
        /// When the collection of pins changed
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach(TKCustomMapPin pin in e.NewItems)
                {
                    this.Map.AddAnnotation(new TKCustomMapAnnotation(pin));
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKCustomMapPin pin in e.OldItems)
                {
                    if (!this.FormsMap.CustomPins.Contains(pin))
                    {
                        var annotation = this.Map.Annotations
                            .OfType<TKCustomMapAnnotation>()
                            .SingleOrDefault(i => i.CustomPin.Equals(pin));

                        if (annotation != null)
                        {
                            annotation.CustomPin.PropertyChanged -= OnPinPropertyChanged;
                            this.Map.RemoveAnnotation(annotation);
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (TKCustomMapAnnotation annotation in this.Map.Annotations)
                {
                    annotation.CustomPin.PropertyChanged -= OnPinPropertyChanged;
                }
                this._firstUpdate = true;
                this.UpdatePins();
            }
        }
        /// <summary>
        /// When the accessory control of a callout gets tapped
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMapCalloutAccessoryControlTapped(object sender, MKMapViewAccessoryTappedEventArgs e)
        {
            if (this.FormsMap.CalloutClickedCommand.CanExecute(null))
            {
                this.FormsMap.CalloutClickedCommand.Execute(null);
            }
        } 
        /// <summary>
        /// When the drag state changed
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnChangedDragState(object sender, MKMapViewDragStateEventArgs e)
        {
            var annotation = e.AnnotationView.Annotation as TKCustomMapAnnotation;
            if (annotation == null) return;

            if (e.NewState == MKAnnotationViewDragState.Starting)
            {
                this._isDragging = true;
            }
            else if (e.NewState == MKAnnotationViewDragState.Dragging)
            {
                annotation.CustomPin.Position = e.AnnotationView.Annotation.Coordinate.ToPosition();
            }
            else if (e.NewState == MKAnnotationViewDragState.Ending || e.NewState == MKAnnotationViewDragState.Canceling)
            {
                e.AnnotationView.DragState = MKAnnotationViewDragState.None;
                this._isDragging = false;
                if (this.FormsMap.PinDragEndCommand != null && this.FormsMap.PinDragEndCommand.CanExecute(annotation.CustomPin))
                {
                    this.FormsMap.PinDragEndCommand.Execute(annotation.CustomPin);
                }
            }
        }
        /// <summary>
        /// When the camera region changed
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnMapRegionChanged(object sender, MKMapViewChangeEventArgs e)
        {
            this.FormsMap.MapCenter = this.Map.CenterCoordinate.ToPosition();
        }
        /// <summary>
        /// When an annotation view got selected
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnDidSelectAnnotationView(object sender, MKAnnotationViewEventArgs e)
        {
            var pin = e.View.Annotation as TKCustomMapAnnotation;
            if(pin == null) return;

            this._selectedAnnotation = e.View.Annotation;
            this.FormsMap.SelectedPin = pin.CustomPin;
            
            if (this.FormsMap.PinSelectedCommand != null && this.FormsMap.PinSelectedCommand.CanExecute(pin.CustomPin))
            {
                this.FormsMap.PinSelectedCommand.Execute(pin.CustomPin);
            }
        }
        /// <summary>
        /// When a tap was perfomed on the map
        /// </summary>
        /// <param name="recognizer">The gesture recognizer</param>
        private void OnMapClicked(UITapGestureRecognizer recognizer)
        {
            if (recognizer.State != UIGestureRecognizerState.Ended) return;

            var pixelLocation = recognizer.LocationInView(this.Map);
            var coordinate = this.Map.ConvertPoint(pixelLocation, this.Map);

            if (this.FormsMap.MapClickedCommand != null && this.FormsMap.MapClickedCommand.CanExecute(coordinate.ToPosition()))
            {
                this.FormsMap.MapClickedCommand.Execute(coordinate.ToPosition());
            }

        }
        /// <summary>
        /// When a long press was performed
        /// </summary>
        /// <param name="recognizer">The gesture recognizer</param>
        private void OnMapLongPress(UILongPressGestureRecognizer recognizer)
        {
            if (recognizer.State != UIGestureRecognizerState.Began) return;

            var pixelLocation = recognizer.LocationInView(this.Map);
            var coordinate = this.Map.ConvertPoint(pixelLocation, this.Map);

            if (this.FormsMap.MapLongPressCommand != null && this.FormsMap.MapLongPressCommand.CanExecute(coordinate.ToPosition()))
            {
                this.FormsMap.MapLongPressCommand.Execute(coordinate.ToPosition());
            }
        }
        /// <summary>
        /// Get the view for the annotation
        /// </summary>
        /// <param name="mapView">The map</param>
        /// <param name="annotation">The annotation</param>
        /// <returns>The annotation view</returns>
        private MKAnnotationView GetViewForAnnotation(MKMapView mapView, IMKAnnotation annotation)
        {
            var customAnnotation = annotation as TKCustomMapAnnotation;

            if (customAnnotation == null) return null;

            MKAnnotationView annotationView;
            if(customAnnotation.CustomPin.Image != null)
                annotationView = mapView.DequeueReusableAnnotation(AnnotationIdentifier);
            else
                annotationView = mapView.DequeueReusableAnnotation(AnnotationIdentifierDefaultPin);
            
            if (annotationView == null)
            {
                if(customAnnotation.CustomPin.Image != null)
                    annotationView = new MKAnnotationView();
                else
                    annotationView = new MKPinAnnotationView(customAnnotation, AnnotationIdentifier);
            }
            else 
            {
                annotationView.Annotation = customAnnotation;
            }
            annotationView.CanShowCallout = customAnnotation.CustomPin.ShowCallout;
            annotationView.Draggable = customAnnotation.CustomPin.IsDraggable;
            annotationView.Selected = this._selectedAnnotation != null && customAnnotation.Equals(this._selectedAnnotation);
            this.SetAnnotationViewVisibility(annotationView, customAnnotation.CustomPin);
            this.UpdateImage(annotationView, customAnnotation.CustomPin);

            if (FormsMap.CalloutClickedCommand != null)
            {
                var button = new UIButton(UIButtonType.InfoLight);
                button.Frame = new CGRect(0, 0, 23, 23);
                button.HorizontalAlignment = UIControlContentHorizontalAlignment.Center;
                button.VerticalAlignment = UIControlContentVerticalAlignment.Center;
                annotationView.RightCalloutAccessoryView = button;
            }
            
            return annotationView;
        }
        /// <summary>
        /// Creates the annotations
        /// </summary>
        private void UpdatePins()
        {
            this.Map.RemoveAnnotations(this.Map.Annotations);

            if (this.FormsMap.CustomPins == null || !this.FormsMap.CustomPins.Any()) return;

            foreach (var i in FormsMap.CustomPins)
            {
                if (this._firstUpdate)
                {
                    i.PropertyChanged += OnPinPropertyChanged;
                }
                var pin = new TKCustomMapAnnotation(i);
                this.Map.AddAnnotation(pin);
            }
            this._firstUpdate = false;

            if (this.FormsMap.PinsReadyCommand != null && this.FormsMap.PinsReadyCommand.CanExecute(this.FormsMap))
            {
                this.FormsMap.PinsReadyCommand.Execute(this.FormsMap);
            }
        }
        /// <summary>
        /// Creates the routes
        /// </summary>
        private void UpdateRoutes(bool firstUpdate = true)
        {
            if (this._routes.Any())
            {
                this.Map.RemoveOverlays(this._routes.Select(i => i.Key).ToArray());
                this._routes.Clear();
            }

            if (this.FormsMap.Routes == null) return;

            foreach (var route in this.FormsMap.Routes)
            {
                this.AddRoute(route);
            }

            if (firstUpdate)
            {
                var observAble = this.FormsMap.Routes as ObservableCollection<TKRoute>;
                if (observAble != null)
                {
                    observAble.CollectionChanged += OnRouteCollectionChanged;
                }
            }
        }
        /// <summary>
        /// Creates the circles on the map
        /// </summary>
        private void UpdateCircles(bool firstUpdate = true)
        {
            if (this._circles.Any())
            {
                this.Map.RemoveOverlays(this._circles.Select(i => i.Key).ToArray());
                this._circles.Clear();
            }

            if (this.FormsMap.Circles == null) return;

            foreach (var circle in this.FormsMap.Circles)
            {
                this.AddCircle(circle);
            }
            if (firstUpdate)
            {
                var observAble = this.FormsMap.Circles as ObservableCollection<TKCircle>;
                if (observAble != null)
                {
                    observAble.CollectionChanged += OnCirclesCollectionChanged;
                }
            }
        }
        /// <summary>
        /// Create the polygons
        /// </summary>
        /// <param name="firstUpdate">If the collection updates the first time</param>
        private void UpdatePolygons(bool firstUpdate = true)
        {
            if (this._polygons.Any())
            {
                this.Map.RemoveOverlays(this._polygons.Select(i => i.Key).ToArray());
                this._polygons.Clear();
            }

            if (this.FormsMap.Polygons == null) return;

            foreach (var poly in this.FormsMap.Polygons)
            {
                this.AddPolygon(poly);
            }
            if (firstUpdate)
            {
                var observAble = this.FormsMap.Polygons as ObservableCollection<TKPolygon>;
                if (observAble != null)
                {
                    observAble.CollectionChanged += OnPolygonsCollectionChanged;
                }
            }
        }
        /// <summary>
        /// When the collection of polygons changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnPolygonsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKPolygon poly in e.NewItems)
                {
                    this.AddPolygon(poly);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKPolygon poly in e.OldItems)
                {
                    if (!this.FormsMap.Polygons.Contains(poly))
                    {
                        poly.PropertyChanged -= OnPolygonPropertyChanged;

                        var item = this._polygons.SingleOrDefault(i => i.Value.Equals(poly));
                        if (item.Key != null)
                        {
                            this.Map.RemoveOverlay(item.Key);
                            this._polygons.Remove(item.Key);
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var poly in this._polygons)
                {
                    poly.Value.PropertyChanged -= OnPolygonPropertyChanged;
                }
                this.UpdatePolygons(false);
            }
        }
        /// <summary>
        /// Adds a polygon to the map
        /// </summary>
        /// <param name="polygon">Polygon to add</param>
        private void AddPolygon(TKPolygon polygon)
        {
            var mkPolygon = MKPolygon.FromCoordinates(polygon.Coordinates.Select(i => i.ToLocationCoordinate()).ToArray());
            this._polygons.Add(mkPolygon, polygon);
            this.Map.AddOverlay(mkPolygon);

            polygon.PropertyChanged += OnPolygonPropertyChanged;
        }
        /// <summary>
        /// When a property of a polygon changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnPolygonPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var poly = (TKPolygon)sender;

            if (poly == null) return;

            var item = this._polygons.SingleOrDefault(i => i.Value.Equals(poly));
            if (item.Key == null) return;

            this.Map.RemoveOverlay(item.Key);
            this._polygons.Remove(item.Key);

            var mkPolygon = MKPolygon.FromCoordinates(poly.Coordinates.Select(i => i.ToLocationCoordinate()).ToArray());
            this._polygons.Add(mkPolygon, poly);
            this.Map.AddOverlay(mkPolygon);
        }
        /// <summary>
        /// When the circles collection changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnCirclesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKCircle circle in e.NewItems)
                {
                    this.AddCircle(circle);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKCircle circle in e.OldItems)
                {
                    if (!this.FormsMap.Circles.Contains(circle))
                    {
                        circle.PropertyChanged -= OnCirclePropertyChanged;

                        var item = this._circles.SingleOrDefault(i => i.Value.Equals(circle));
                        if (item.Key != null)
                        {
                            this.Map.RemoveOverlay(item.Key);
                            this._circles.Remove(item.Key);
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var circle in this._circles)
                {
                    circle.Value.PropertyChanged -= OnCirclePropertyChanged;
                }
                this.UpdateCircles(false);
            }
        }
        /// <summary>
        /// When the route collection changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnRouteCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TKRoute route in e.NewItems)
                {
                    this.AddRoute(route);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (TKRoute route in e.OldItems)
                {
                    if (!this.FormsMap.Routes.Contains(route))
                    {
                        route.PropertyChanged -= OnRoutePropertyChanged;

                        var item = this._routes.SingleOrDefault(i => i.Value.Equals(route));
                        if (item.Key != null)
                        {
                            this.Map.RemoveOverlay(item.Key);
                            this._routes.Remove(item.Key);
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var route in this._routes)
                {
                    route.Value.PropertyChanged -= OnRoutePropertyChanged;
                }
                this.UpdateRoutes(false);
            }
        }
        /// <summary>
        /// Adds a route
        /// </summary>
        /// <param name="route">The route to add</param>
        private void AddRoute(TKRoute route)
        {
            var polyLine = MKPolyline.FromCoordinates(route.RouteCoordinates.Select(i => i.ToLocationCoordinate()).ToArray());
            this._routes.Add(polyLine, route);
            this.Map.AddOverlay(polyLine);

            route.PropertyChanged += OnRoutePropertyChanged;
        }
        /// <summary>
        /// Adds a circle to the map
        /// </summary>
        /// <param name="circle">The circle to add</param>
        private void AddCircle(TKCircle circle)
        {
            var mkCircle = MKCircle.Circle(circle.Center.ToLocationCoordinate(), circle.Radius);
            this._circles.Add(mkCircle, circle);
            this.Map.AddOverlay(mkCircle);

            circle.PropertyChanged += OnCirclePropertyChanged;
        }
        /// <summary>
        /// When a property of a circle changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnCirclePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var circle = (TKCircle)sender;

            if (circle == null) return;

            var item = this._circles.SingleOrDefault(i => i.Value.Equals(circle));
            if (item.Key == null) return;

            this.Map.RemoveOverlay(item.Key);
            this._circles.Remove(item.Key);

            var mkCircle = MKCircle.Circle(circle.Center.ToLocationCoordinate(), circle.Radius);
            this._circles.Add(mkCircle, circle);
            this.Map.AddOverlay(mkCircle);
        }
        /// <summary>
        /// When a property of the route changes, re-add the <see cref="MKPolyline"/>
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnRoutePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var route = (TKRoute)sender;

            if(route == null) return;

            var item = this._routes.SingleOrDefault(i => i.Value.Equals(route));
            if (item.Key == null) return;

            this.Map.RemoveOverlay(item.Key);
            this._routes.Remove(item.Key);

            var polyLine = MKPolyline.FromCoordinates(route.RouteCoordinates.Select(i => i.ToLocationCoordinate()).ToArray());
            this._routes.Add(polyLine, route);
            this.Map.AddOverlay(polyLine);
        }
        /// <summary>
        /// When a property of the pin changed
        /// </summary>
        /// <param name="sender">Event Sender</param>
        /// <param name="e">Event Arguments</param>
        private void OnPinPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == TKCustomMapPin.TitlePropertyName ||
                e.PropertyName == TKCustomMapPin.SubititlePropertyName ||
                (e.PropertyName == TKCustomMapPin.PositionPropertyName && this._isDragging))
                return;

            var formsPin = (TKCustomMapPin)sender;
            var annotation = this.Map.Annotations
                .OfType<TKCustomMapAnnotation>()
                .SingleOrDefault(i => i.CustomPin.Equals(formsPin));

            if (annotation == null) return;

            var annotationView = this.Map.ViewForAnnotation(annotation);
            if (annotationView == null) return;

            switch (e.PropertyName)
            {
                case TKCustomMapPin.ImagePropertyName:
                    this.UpdateImage(annotationView, formsPin);
                    break;
                case TKCustomMapPin.IsDraggablePropertyName:
                    annotationView.Draggable = formsPin.IsDraggable;
                    break;
                case TKCustomMapPin.IsVisiblePropertyName:
                    this.SetAnnotationViewVisibility(annotationView, formsPin);
                    break;
                case TKCustomMapPin.PositionPropertyName:
                    annotation.SetCoordinate(formsPin.Position.ToLocationCoordinate());
                    break;
                case TKCustomMapPin.ShowCalloutPropertyName:
                    annotationView.CanShowCallout = formsPin.ShowCallout;
                    break;
            }
        }
        /// <summary>
        /// Set the visibility of an annotation view
        /// </summary>
        /// <param name="annotationView">The annotation view</param>
        /// <param name="pin">The forms pin</param>
        private void SetAnnotationViewVisibility(MKAnnotationView annotationView, TKCustomMapPin pin)
        {
            annotationView.Hidden = !pin.IsVisible;
            annotationView.UserInteractionEnabled = pin.IsVisible;
            annotationView.Enabled = pin.IsVisible;
        }
        /// <summary>
        /// Set the image of the annotation view
        /// </summary>
        /// <param name="annotationView">The annotation view</param>
        /// <param name="pin">The forms pin</param>
        private async void UpdateImage(MKAnnotationView annotationView, TKCustomMapPin pin)
        {
            if (pin.Image != null)
            {
                // If this is the case, we need to get a whole new annotation view. 
                if (annotationView.GetType() == typeof (MKPinAnnotationView))
                {
                    this.Map.RemoveAnnotation(annotationView.Annotation);
                    this.Map.AddAnnotation(new TKCustomMapAnnotation(pin));
                    return;
                }

                var image = await new ImageLoaderSourceHandler().LoadImageAsync(pin.Image);
                Device.BeginInvokeOnMainThread(() =>
                {
                    annotationView.Image = image;
                });
            }
            else
            {
                var pinAnnotationView = annotationView as MKPinAnnotationView;
                if (pinAnnotationView != null)
                {
                    pinAnnotationView.AnimatesDrop = true;
                    pinAnnotationView.PinTintColor = UIColor.Red;
                }
            }
        }
        /// <summary>
        /// Sets the selected pin
        /// </summary>
        private void SetSelectedPin()
        {
            var customAnnotion = this._selectedAnnotation as TKCustomMapAnnotation;

            if (customAnnotion != null)
            {
                if (customAnnotion.CustomPin.Equals(this.FormsMap.SelectedPin)) return;

                var annotationView = this.Map.ViewForAnnotation(customAnnotion);
                annotationView.Selected = false;

                this._selectedAnnotation = null;
            }
            if (this.FormsMap.SelectedPin != null)
            {
                var selectedAnnotation = this.Map.Annotations
                    .OfType<TKCustomMapAnnotation>()
                    .SingleOrDefault(i => i.CustomPin.Equals(this.FormsMap.SelectedPin));

                if (selectedAnnotation != null)
                {
                    var annotationView = this.Map.ViewForAnnotation(selectedAnnotation);
                    if (annotationView != null)
                    {
                        annotationView.Selected = true;
                    }
                    this._selectedAnnotation = selectedAnnotation;

                    if (this.FormsMap.PinSelectedCommand != null && this.FormsMap.PinSelectedCommand.CanExecute(null))
                    {
                        this.FormsMap.PinSelectedCommand.Execute(null);
                    }
                }
            }
        }
        /// <summary>
        /// Sets the center of the map
        /// </summary>
        private void SetMapCenter()
        {
            if (!this.FormsMap.MapCenter.Equals(this.Map.CenterCoordinate.ToPosition()))
            {
                this.Map.SetCenterCoordinate(this.FormsMap.MapCenter.ToLocationCoordinate(), this.FormsMap.AnimateMapCenterChange);   
            }
        }
    }
}
