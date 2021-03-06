# Extended Map Control for Xamarin Forms Maps

## NuGet

https://www.nuget.org/packages/TK.CustomMap/

## Features

* Bindable Pins
 * Change Image of Pins
 * Make Pins Draggable
 * Hide Pins
* Bindable Selected Pin
* Bindable Map Center
* Bindable Routes
* Bindable Circles
* Bindable Polygons
* Map Commands
  * Long Press
  * Click
  * Pin Selected
  * Drag End
  * Pins Ready
  * Callout click

### Example

#### Android

Make sure you have set the following permissions included in the manifest:

```XML
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" />
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
```

Set your Google Maps API Key:
```XML
<application>
	<meta-data android:name="com.google.android.maps.v2.API_KEY" android:value="YOUR API KEY" />
</application>
```

#### iOS

Make sure you call ```TKCustomMapRenderer.InitMapRenderer();```

#### XAML

```XAML
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://xamarin.com/schemas/2014/forms"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:tkmap="clr-namespace:TK.CustomMap;assembly=TK.CustomMap"
             x:Class="TK.CustomMap.Sample.SamplePage">
  <StackLayout>
    <SearchBar />
    <tkmap:TKCustomMap 
      CustomPins="{Binding Pins}" 
      MapClickedCommand="{Binding MapClickedCommand}" 
      MapLongPressCommand="{Binding MapLongPressCommand}" 
      MapCenter="{Binding MapCenter}" 
      AnimateMapCenterChange="True"
      PinSelectedCommand="{Binding PinSelectedCommand}"
      SelectedPin="{Binding SelectedPin}"
      Routes="{Binding Routes}"
      PinDragEndCommand="{Binding DragEndCommand}"
      Circles="{Binding Circles}"
      CalloutClickedCommand="{Binding CalloutClickedCommand}"
      Polygons="{Binding Polygons}"/> 
  </StackLayout>
</ContentPage>
```

#### CSharp

```C#
var mapView = new TKCustomMap();
mapView.SetBinding(TKCustomMap.CustomPinsProperty, "Pins");
mapView.SetBinding(TKCustomMap.MapClickedCommandProperty, "MapClickedCommand");
mapView.SetBinding(TKCustomMap.MapLongPressCommandProperty, "MapLongPressCommand");
mapView.SetBinding(TKCustomMap.MapCenterProperty, "MapCenter");
mapView.SetBinding(TKCustomMap.PinSelectedCommandProperty, "PinSelectedCommand");
mapView.SetBinding(TKCustomMap.SelectedPinProperty, "SelectedPin");
mapView.SetBinding(TKCustomMap.RoutesProperty, "Routes");
mapView.SetBinding(TKCustomMap.PinDragEndCommandProperty, "DragEndCommand");
mapView.SetBinding(TKCustomMap.CirclesProperty, "Circles");
mapView.SetBinding(TKCustomMap.CalloutClickedCommandProperty, "CalloutClickedCommand");
mapView.SetBinding(TKCustomMap.PolygonsProperty, "Polygons");
mapView.AnimateMapCenterChange = true;
```

### Extra Features

* Google Maps Places API Wrapper(API Key needed) https://developers.google.com/places/
 * Get Place predictions
 * Get Place details
* Google Maps Directions API Wrapper(API Key needed) https://developers.google.com/maps/documentation/directions/
 * Calculate Routes
* OSM Nominatim API Wrapper http://wiki.openstreetmap.org/wiki/Nominatim
 * Get Place predictions

### Example

#### Set API Key

You need to set your Google Maps Places API Key before you can perform any call. You only need to do this once. An appropriate place would be your App constructor.

```C#
public App()
{
    GmsPlace.Init("Your API Key");
    GmsDirection.Init("YOUR API KEY");

    // The root page of your application
    MainPage = new SamplePage();
}
```

#### Predictions and Details

To get a list of predictions(to fill an Autocomplete for example):

```C#
// Google Maps Places
GmsPlaceResult predictions = await GmsPlace.Instance.GetPredictions("Sydney");
// OSM Nominatim
IEnumerable<OsmNominatimResult> predictions = await OsmNominatim.Instance.GetPredictions("Sydney");
```

To get the details of a prediction:

```C#
GmsDetailsResult details = await GmsPlace.Instance.GetDetails(predictions.Predictions[0].PlaceId);
```

## Video

### Android

[![Android](http://i.imgur.com/HDrntbk.png)](http://youtu.be/r2Sm6F15nMU "Android")

### iOS

[![iOS](http://i.imgur.com/q8uuh7q.png)](https://youtu.be/J7Ud6JHmWUM "iOS")
