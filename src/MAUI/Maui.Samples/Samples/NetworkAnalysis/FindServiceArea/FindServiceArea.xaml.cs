// Copyright 2022 Esri.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at: http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific
// language governing permissions and limitations under the License.

using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.NetworkAnalysis;
using Esri.ArcGISRuntime.UI;

namespace ArcGIS.Samples.FindServiceArea
{
    [ArcGIS.Samples.Shared.Attributes.Sample(
        name: "Find service area",
        category: "Network analysis",
        description: "Find the service area within a network from a given point.",
        instructions: "In order to find any service areas at least one facility needs to be added to the map view.",
        tags: new[] { "barriers", "facilities", "impedance", "logistics", "routing" })]
    public partial class FindServiceArea
    {
        // Uri for the service area around San Diego.
        private Uri _serviceAreaUri = new Uri("https://sampleserver6.arcgisonline.com/arcgis/rest/services/NetworkAnalysis/SanDiego/NAServer/ServiceArea");

        public FindServiceArea()
        {
            InitializeComponent();

            // Create the map, graphics overlay, and sketch editor.
            Initialize();
        }

        private void Initialize()
        {
            // Center the map on San Diego.
            Map streetsMap = new Map(BasemapStyle.ArcGISLightGray)
            {
                InitialViewpoint = new Viewpoint(32.73, -117.14, 30000)
            };
            MyMapView.Map = streetsMap;

            // Create graphics overlays for all of the elements of the map.
            MyMapView.GraphicsOverlays.Add(new GraphicsOverlay());

            // Add a new behavior for double taps on the MapView.
            MyMapView.GeoViewDoubleTapped += (s, e) =>
            {
                // If the sketch editor complete command is enabled, a sketch is in progress.
                if (MyMapView.SketchEditor.CompleteCommand.CanExecute(null))
                {
                    // Set the event as handled.
                    e.Handled = true;

                    // Finish any drawing.
                    MyMapView.SketchEditor.CompleteCommand.Execute(null);

                    // Reset the UI.
                    DrawBarrierButton.Text = "Draw barrier";
                }
            };
        }

        private async void PlaceFacilityButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Let the user tap on the map view using the point sketch mode.
                SketchCreationMode creationMode = SketchCreationMode.Point;
                Geometry geometry = await MyMapView.SketchEditor.StartAsync(creationMode, false);

                // Symbology for a facility.
                PictureMarkerSymbol facilitySymbol = new PictureMarkerSymbol(new Uri("https://static.arcgis.com/images/Symbols/SafetyHealth/Hospital.png"))
                {
                    Height = 30,
                    Width = 30
                };

                // Create a graphic for the facility.
                Graphic facilityGraphic = new Graphic(geometry, new Dictionary<string, object>() { { "Type", "Facility" } }, facilitySymbol)
                {
                    ZIndex = 2
                };

                // Add the graphic to the graphics overlay.
                MyMapView.GraphicsOverlays[0].Graphics.Add(facilityGraphic);
            }
            catch (TaskCanceledException)
            {
                // Ignore this exception.
            }
            catch (Exception ex)
            {
                // Report exceptions.
                await Application.Current.MainPage.DisplayAlert("Error", "Error drawing facility:\n" + ex.Message, "OK");
            }
        }

        private async void DrawBarrierButton_Click(object sender, EventArgs e)
        {
            // Finish drawing barriers if started.
            if (DrawBarrierButton.Text != "Draw barrier")
            {
                if (MyMapView.SketchEditor.CompleteCommand.CanExecute(null))
                    MyMapView.SketchEditor.CompleteCommand.Execute(null);

                DrawBarrierButton.Text = "Draw barrier";
                return;
            }
            try
            {
                // Update the UI.
                DrawBarrierButton.Text = "Finish drawing";

                // Let the user draw on the map view using the polyline sketch mode.
                SketchCreationMode creationMode = SketchCreationMode.Polyline;
                Geometry geometry = await MyMapView.SketchEditor.StartAsync(creationMode, false);

                // Symbol for the barriers.
                SimpleLineSymbol barrierSymbol = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.Black, 5.0f);

                // Create the graphic to be used for barriers.
                Graphic barrierGraphic = new Graphic(geometry, new Dictionary<string, object>() { { "Type", "Barrier" } }, barrierSymbol)
                {
                    ZIndex = 1
                };

                // Add a graphic from the polyline the user drew.
                MyMapView.GraphicsOverlays[0].Graphics.Add(barrierGraphic);
            }
            catch (TaskCanceledException)
            {
                // Ignore this exception.
            }
            catch (Exception ex)
            {
                // Report exceptions.
                await Application.Current.MainPage.DisplayAlert("Error", "Error drawing barrier:\n" + ex.Message, "OK");
            }
        }

        private async void ShowServiceAreasButton_Click(object sender, EventArgs e)
        {
            // Finish any drawing in progress.
            if (MyMapView.SketchEditor.CompleteCommand.CanExecute(null))
            {
                MyMapView.SketchEditor.CompleteCommand.Execute(null);

                DrawBarrierButton.Text = "Draw barrier";
            }

            // Use a local variable for the graphics overlay.
            GraphicCollection allGraphics = MyMapView.GraphicsOverlays[0].Graphics;

            // Get a list of the facilities from the graphics overlay.
            List<ServiceAreaFacility> serviceAreaFacilities = (from g in allGraphics
                                                               where (string)g.Attributes["Type"] == "Facility"
                                                               select new ServiceAreaFacility((MapPoint)g.Geometry)).ToList();

            // Check that there is at least 1 facility to find a service area for.
            if (!serviceAreaFacilities.Any())
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Must have at least one Facility!", "OK");
                return;
            }

            // Create the service area task and parameters based on the Uri.
            ServiceAreaTask serviceAreaTask = await ServiceAreaTask.CreateAsync(_serviceAreaUri);

            // Store the default parameters for the service area in an object.
            ServiceAreaParameters serviceAreaParameters = await serviceAreaTask.CreateDefaultParametersAsync();

            // Add impedance cutoffs for facilities (drive time minutes).
            serviceAreaParameters.DefaultImpedanceCutoffs.Add(2.0);
            serviceAreaParameters.DefaultImpedanceCutoffs.Add(5.0);

            // Set the level of detail for the polygons.
            serviceAreaParameters.PolygonDetail = ServiceAreaPolygonDetail.High;

            // Get a list of the barriers from the graphics overlay.
            List<PolylineBarrier> polylineBarriers = (from g in allGraphics
                                                      where (string)g.Attributes["Type"] == "Barrier"
                                                      select new PolylineBarrier((Polyline)g.Geometry)).ToList();

            // Add the barriers to the service area parameters.
            serviceAreaParameters.SetPolylineBarriers(polylineBarriers);

            // Update the parameters to include all of the placed facilities.
            serviceAreaParameters.SetFacilities(serviceAreaFacilities);

            // Clear existing graphics for service areas.
            foreach (Graphic g in allGraphics.ToList())
            {
                // Check if the graphic g is a service area.
                if ((string)g.Attributes["Type"] == "ServiceArea")
                {
                    allGraphics.Remove(g);
                }
            }

            try
            {
                // Solve for the service area of the facilities.
                ServiceAreaResult result = await serviceAreaTask.SolveServiceAreaAsync(serviceAreaParameters);

                // Loop over each facility.
                for (int i = 0; i < serviceAreaFacilities.Count; i++)
                {
                    // Create list of polygons from a service facility.
                    List<ServiceAreaPolygon> polygons = result.GetResultPolygons(i).ToList();

                    // Symbol for the outline of the service areas.
                    SimpleLineSymbol serviceOutline = new SimpleLineSymbol(SimpleLineSymbolStyle.Solid, System.Drawing.Color.DarkGray, 3.0f);

                    // Create a list of fill symbols for the polygons.
                    List<SimpleFillSymbol> fillSymbols = new List<SimpleFillSymbol>();
                    fillSymbols.Add(new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.FromArgb(70, 255, 0, 0), serviceOutline));
                    fillSymbols.Add(new SimpleFillSymbol(SimpleFillSymbolStyle.Solid, System.Drawing.Color.FromArgb(70, 255, 165, 0), serviceOutline));

                    // Loop over every polygon in every facilities result.
                    for (int j = 0; j < polygons.Count; j++)
                    {
                        // Create the graphic for the service areas, alternating between fill symbols.
                        Graphic serviceGraphic = new Graphic(polygons[j].Geometry, new Dictionary<string, object>() { { "Type", "ServiceArea" } }, fillSymbols[j % 2])
                        {
                            ZIndex = 0
                        };

                        // Add graphic for service area. Alternate the color of each polygon.
                        allGraphics.Add(serviceGraphic);
                    }
                }
            }
            catch (Esri.ArcGISRuntime.Http.ArcGISWebException exception)
            {
                if (exception.Message.ToString().Equals("Unable to complete operation."))
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "Facility not within San Diego area!", "OK");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "An ArcGIS web exception occurred. \n" + exception.Message, "OK");
                }
            }
        }

        private void Reset_Click(object sender, EventArgs e)
        {
            // Clear all of the graphics.
            MyMapView.GraphicsOverlays[0].Graphics.Clear();
        }
    }
}