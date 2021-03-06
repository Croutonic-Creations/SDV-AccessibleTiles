using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace AccessibleTiles.TrackingMode {
    public class Tracker {

        private ModEntry mod;
        //public GameLocation? last_location;

        String? focus_name;
        String? focus_type;

        public bool sort_by_proxy = false;
        public SortedList<string, Dictionary<string, SpecialObject>> focusable = new();

        private SButton read;
        private SButton cycleup;
        private SButton cycledown;
        private SButton readtile;
        private SButton sort_order_toggle;

        public List<string> categories = new();

        public Tracker(ModEntry mod) {
            this.mod = mod;
            this.read = mod.Config.TrackingModeRead;
            this.cycleup = mod.Config.TrackingModeCycleUp;
            this.cycledown = mod.Config.TrackingModeCycleDown;
            this.readtile = mod.Config.TrackingModeGetTile;
            this.sort_order_toggle = mod.Config.TrackingToggleSortingMode;
        }

        public void ScanArea(GameLocation location) {
            ScanArea(location, false);
        }

        public void ScanArea(GameLocation location, Boolean? clear_focus) {

            focusable.Clear();
            categories = new();
            Dictionary<Vector2, (string name, string category)> scannedTiles = mod.stardewAccess.SearchLocation();

            /* Categorise the scanned tiles into groups
             *
             * This method uses breadth first search so the first item is the closest item, no need to reorder or check for closest item
             */
            foreach (var tile in scannedTiles) {
                AddFocusableObject(tile.Value.category, tile.Value.name, tile.Key);
            }

            this.AddSpecialPoints(location);
            this.AddEntrances(location);

            // Sort each category by name if sort by proxy is disabled
            if (!sort_by_proxy) {
                foreach (var cat in focusable) {
                    var ordered = cat.Value.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                    cat.Value.Clear();
                    foreach (var item in ordered) {
                        cat.Value.Add(item.Key, item.Value);
                    }
                }
            }

            if (focus_name == null || focus_type == null || (bool)clear_focus) {
                clearFocus();
            }
        }

        private void AddFocusableObject(string category, string name, Vector2 tile) {

            if (!focusable.ContainsKey(category)) {
                focusable.Add(category, new());
                categories.Add(category);
            }

            SpecialObject sObject = new SpecialObject(name, tile);

            if (!focusable.GetValueOrDefault(category).ContainsKey(name))
                focusable.GetValueOrDefault(category).Add(name, sObject);

        }

        private void AddEntrances(GameLocation location) {

            string category = "entrances";

            focusable.Add(category, new());
            categories.Add(category);

            Dictionary<string, SpecialObject> entrances = TrackerUtility.GetEntrances(this.mod);
            
            foreach(var(name, sObject) in entrances) {
                AddFocusableObject(category, name, sObject.TileLocation);
            }

            if (!focusable[category].Any() == true) {
                focusable.Remove(category);
            }

        }

        private void AddSpecialPoints(GameLocation location) {

            string category = "special";

            focusable.Add(category, new());
            categories.Add(category);

            if (location.Name == "Saloon") {
                AddFocusableObject(category, "Gus's Fridge", new(18, 16));
            } else if (location.Name == "ScienceHouse") {
                AddFocusableObject(category, "Robin's Woodpile", new(11, 19));
            } else if (location.Name == "ArchaeologyHouse") {
                AddFocusableObject(category, "Box for Bone Fragments", new(6, 9));
            } else if (location.Name == "JoshHouse") {
                AddFocusableObject(category, "Evelyn's Stove", new(3, 16));
            } else if (location.Name == "Tunnel") {
                AddFocusableObject(category, "Lock Box", new(17, 6));
            } else if (location is Town) {
                if (Game1.player.hasQuest(31) && !Game1.player.hasMagnifyingGlass) {
                    AddFocusableObject(category, "Shadow Guy's Hiding Bush", new(28, 13));
                }
            } else if (location is Railroad) {
                AddFocusableObject(category, "Recycle Bin", new(28, 36));
                AddFocusableObject(category, "Empty Rainbow Shell Crate", new(45, 40));
            } else if (location is SeedShop) {
                AddFocusableObject(category, "Vegetable Bin", new(19, 28));
            } else if (location is Beach) {
                if(location.currentEvent != null && location.currentEvent.playerControlTargetTile == new Point(53, 8)) {
                    AddFocusableObject(category, "Haley's Bracelet", new(53, 8));
                }
                AddFocusableObject(category, "Willy's Barrel", new(37, 33));
                
            }

            if (!focusable[category].Any() == true) {
                focusable.Remove(category);
            }

        }

        public void clearFocus() {
            foreach (string cat in categories) {
                if (focusable.ContainsKey(cat)) {
                    focus_type = cat;
                    this.focusFirstItemOfCurrentlyFocusedCategory();
                    return;
                }
            }
        }

        private void focusFirstItemOfCurrentlyFocusedCategory() {
            object focus = focusable[focus_type].Values.First();
            focus_name = (focus as SpecialObject).name;
        }

        private void say(string text, bool force) {
            if (mod.stardewAccess != null) {
                mod.stardewAccess.Say(text, force);
            }
        }

        public void ChangeCategory(SButton button) {

            if (focus_type != null && focusable.ContainsKey(focus_type)) {
                int index = focusable.IndexOfKey(focus_type);
                index += button == this.cycledown ? 1 : -1;

                if (index >= 0 && index < focusable.Count) {

                    focus_type = focusable.Keys[index];
                    mod.console.Debug("Change Category: " + focus_type);

                    object focus = focusable[focus_type].Values.First();
                    focus_name = (focus as SpecialObject).name;

                }
                this.say(focus_type + " Category,  Focus - " + focus_name, true);

            } else {
                mod.console.Debug(focusable.ToArray().ToString());
                this.say("No Categories Found", true);
            }
        }

        internal void OnButtonPressed(object sender, ButtonPressedEventArgs e) {

            if (mod.stardewAccess == null || Game1.activeClickableMenu != null) {
                return;
            }

            if (e.Button == this.read) {
                if (e.IsDown(SButton.LeftControl) || e.IsDown(SButton.RightControl)) {
                    ReadCurrentFocus(false, true, false);
                } else {
                    ReadCurrentFocus(false, false, false);
                }
            }

            if (e.Button == this.readtile) {
                if (e.IsDown(SButton.LeftControl) || e.IsDown(SButton.RightControl)) {
                    ReadCurrentFocus(true, true, false);
                } else {
                    ReadCurrentFocus(true, false, false);
                }

            }

            if (e.Button == this.cycleup || e.Button == this.cycledown) {
                if (e.IsDown(SButton.LeftControl) || e.IsDown(SButton.RightControl)) {
                    ChangeCategory(e.Button);
                } else {
                    ChangeFocus(e.Button);
                }

            }

            if (e.Button == this.sort_order_toggle) {
                mod.console.Debug("Change Sorting... " + sort_by_proxy);
                bool prev_proxy = sort_by_proxy;
                foreach (string key in focusable.Keys) {
                    if (prev_proxy == true) {
                        sort_by_proxy = false;

                        // Refresh the list
                        mod.console.Debug("Refreshing...");
                        this.ScanArea(Game1.currentLocation);
                        this.focusFirstItemOfCurrentlyFocusedCategory();

                        this.say($"Sorting by Name, {focus_name} focused", true);
                        mod.console.Debug($"Focused on {focus_name}");
                        break;
                    } else {
                        sort_by_proxy = true;

                        // Refresh the list
                        mod.console.Debug("Refreshing...");
                        this.ScanArea(Game1.currentLocation);
                        this.focusFirstItemOfCurrentlyFocusedCategory();

                        this.say($"Sorting by Proximity, {focus_name} focuse", true);
                        mod.console.Debug($"Focused on {focus_name}");
                        break;
                    }
                }
                mod.console.Debug("Proxy Sort: " + sort_by_proxy.ToString());
            }

        }

        private void ChangeFocus(SButton? button, int? key, string? extra_details) {

            if (focusable.Count() < 1 || !focusable.ContainsKey(focus_type)) {
                this.say("Nothing Found.", true);
                return;
            }

            Dictionary<string, SpecialObject> local_focusable = focusable[focus_type];

            if (extra_details == null) {
                extra_details = "";
            }

            string extra_details_end = "";

            if (key == null) {
                if (button == null) {
                    return;
                }
                int direction = button == this.cycleup ? -1 : 1;
                key = local_focusable.Keys.ToList().IndexOf(focus_name) + direction;
            }

            if (key < 0 || key > local_focusable.Count - 1) {
                //end of list
                extra_details_end += "End of list, ";
            } else {
                object focus = local_focusable.Values.ElementAt((int)key);
                focus_name = (focus as SpecialObject).name;
            }

            this.say(extra_details + $"{focus_name} focused, " + extra_details_end, true);
            mod.console.Debug(extra_details + $"Focused on {focus_name}., " + extra_details_end);
        }

        private void ChangeFocus(SButton button) {
            this.ChangeFocus(button, null, null);
        }

        public Dictionary<string, (NPC, int)> controlled_npcs = new();

        private void ReadCurrentFocus(bool tileOnly, bool autopath, bool faceDirection) {
            if (focus_name != null && focus_type != null) {

                Farmer player = Game1.player;

                mod.console.Debug("run scan");
                ScanArea(player.currentLocation);

                if (!focusable.ContainsKey(focus_type)) {
                    clearFocus();
                    return;
                }

                Dictionary<string, SpecialObject> local_focusable = focusable[focus_type];

                if (!local_focusable.ContainsKey(focus_name)) {
                    this.ChangeFocus(null, 0, $"Can't find {focus_name}, ");
                    return;
                }

                Vector2 position = player.getTileLocation();

                SpecialObject focus = local_focusable[focus_name];
                Vector2 location = focus.TileLocation;
                //str += $"{focus_name} at {focus.TileLocation.X}-{focus.TileLocation.Y}, ";

                if (focus == null || location == Vector2.Zero) {
                    mod.console.Debug("focus is null or location is zero. " + location.ToString());
                    return;
                }

                Vector2 tileXY = location;

                location.X += Game1.tileSize / 4;
                location.Y += Game1.tileSize / 4;

                string direction = TrackerUtility.GetDirection(player.getTileLocation(), tileXY);
                if (faceDirection) {
                    if (direction == "North") {
                        player.faceDirection(0);
                    }
                    if (direction == "East") {
                        player.faceDirection(1);
                    }
                    if (direction == "South") {
                        player.faceDirection(2);
                    }
                    if (direction == "West") {
                        player.faceDirection(3);
                    }
                }

                double distance = Math.Round(TrackerUtility.GetDistance(player.getTileLocation(), tileXY));

                //Game1.currentLocation.TemporarySprites.Add(new TemporaryAnimatedSprite("LooseSprites\\Cursors", new Rectangle(346, 400, 8, 8), 10f, 1, 50, tileXY, flicker: false, flipped: false, layerDepth: 999, 0f, Color.White, 4f, 0f, 0f, 0f));
                //autopath = false;
                if (focus.reachable != false) {
                    if (autopath) {
                        Vector2? closest_tile = null;
                        if (focus.PathfindingOverride != null) {
                            closest_tile = focus.PathfindingOverride;
                        } else {
                            closest_tile = GetClosestTile(tileXY);
                        }

                        mod.console.Debug($"closest tile: {closest_tile}");
                        if (closest_tile != null) {
                            Vector2 tile = (Vector2)closest_tile;

                            if (tileOnly) { //get directions

                                mod.console.Debug("Get Directions to " + tile);
                                string[] cardinal_directions = { };
                                PathFindController controller = new PathFindController(player, Game1.currentLocation, new Point((int)tile.X, (int)tile.Y), -1);

                                Vector2 last_tile = player.getTileLocation();
                                controller.pathToEndPoint.Pop(); //remove current tile
                                foreach (Point p in controller.pathToEndPoint) {
                                    Vector2 new_tile = new(p.X, p.Y);

                                    cardinal_directions = cardinal_directions.Append(TrackerUtility.GetDirection(last_tile, new_tile)).ToArray();
                                    last_tile = new_tile;
                                }

                                string directions = String.Join(" - ", TrackerUtility.get_directions(cardinal_directions));
                                say($"{focus_name} at {directions}");
                            } else {
                                player.UsingTool = false;
                                player.controller = new PathFindController(player, Game1.currentLocation, new Point((int)tile.X, (int)tile.Y), -1, (Character farmer, GameLocation location) => {
                                    direction = TrackerUtility.GetDirection(player.getTileLocation(), tile);
                                    ReadCurrentFocus(false, false, true);
                                    mod.movingWithTracker = false;
                                    Task ignore = UnhaltNPCS();
                                    player.canMove = true;
                                    mod.Helper.ConsoleCommands.Trigger("debug", arguments: new string[] { "cm" });
                                });
                                this.say($"moving near {focus_name}, to {tile.X}-{tile.Y}", true);
                                mod.movingWithTracker = true;
                            }
                        } else {
                            this.say($"Could not find path to {focus_name} at {tileXY.X}-{tileXY.Y}.", true);
                        }
                    } else {
                        if (tileOnly) {
                            this.say($"{focus_name} is at {tileXY.X}-{tileXY.Y}, player is at {position.X}-{position.Y}", true);
                        } else {
                            this.say($"{focus_name} is {direction} {distance} tiles, at {tileXY.X}-{tileXY.Y}, player is at {position.X}-{position.Y}", true);
                        }
                    }
                } else {
                    this.say(focus.unreachable_reason, true);
                    mod.console.Debug(focus.unreachable_reason);
                }


            }

        }

        private void say(string text) {
            if (mod.stardewAccess != null) {
                mod.stardewAccess.Say(text, true);
            }
            mod.console.Debug(text);
        }

        public async Task UnhaltNPCS() {
            await Task.Delay(3000);
            foreach (var key_value in controlled_npcs) {
                (NPC, int) npc = key_value.Value;
                npc.Item1.speed = npc.Item2;
            }
            controlled_npcs.Clear();
        }

        private Vector2? GetClosestTile(Vector2 tileXY) {

            int radius = 3;
            int layers = radius - 2;

            Vector2 topLeft = new(tileXY.X - layers, tileXY.Y - layers);
            Vector2 bottomRight = new(tileXY.X + layers, tileXY.Y + layers);

            Vector2? closest_tile = null;
            double? closest_tile_distance = null;
            double? closest_tile_distance_to_object = null;

            //store the locations currently being checked
            Dictionary<string, Vector2> checks = new Dictionary<string, Vector2>();

            //first, check the spot directly next to the object, closest to where the player is standing
            //to add more checks, increase i in the for loop below and add another branch to the first if statement
            checks.Add("top", Vector2.Add(topLeft, new(1, 0)));
            checks.Add("right", Vector2.Add(topLeft, new(2, 1)));
            checks.Add("bottom", Vector2.Add(topLeft, new(1, 2)));
            checks.Add("left", Vector2.Add(topLeft, new(0, 1)));

            for (int i = 0; i <= 1; i++) {

                if(i == 1) {
                    //scan corners, no good location for direct contact
                    checks.Clear();

                    checks.Add("topLeft", topLeft);
                    checks.Add("topRight", Vector2.Add(bottomRight, new(2, 0)));
                    checks.Add("bottomRight", bottomRight);
                    checks.Add("bottomLeft", Vector2.Add(topLeft, new(0, 2)));
                    
                }

                foreach (var (qualifier, tile) in checks) {

                    PathFindController controller = new PathFindController(Game1.player, Game1.currentLocation, new Point((int)tile.X, (int)tile.Y), -1, eraseOldPathController: true);

                    if (controller.pathToEndPoint != null) {

                        int tile_distance = controller.pathToEndPoint.Count();
                        double distance_to_object = TrackerUtility.GetDistance(tileXY, Game1.player.getTileLocation());

                        if (closest_tile_distance == null) {
                            closest_tile = tile;
                            closest_tile_distance = tile_distance;
                            closest_tile_distance_to_object = distance_to_object;
                        }

                        if (tile_distance <= closest_tile_distance && distance_to_object <= closest_tile_distance_to_object) {

                            if (closest_tile == null) {
                                closest_tile = tile;
                                continue;
                            }

                            closest_tile = tile;
                            closest_tile_distance = tile_distance;
                            closest_tile_distance_to_object = distance_to_object;

                        }
                    }
                }

                if (closest_tile != null) {
                    return closest_tile;
                }

            }

            return null;
        }
    }

}

public class SpecialObject {

    public string name;
    public Vector2 TileLocation;
    public Vector2? PathfindingOverride;

    public NPC? character;
    public bool reachable = true;

    public string? unreachable_reason;

    public SpecialObject(string name) {
        this.name = name;
    }

    public SpecialObject(string name, Vector2 location) {
        this.name = name;
        this.TileLocation = location;
    }

    public SpecialObject(string name, Vector2 location, Vector2 path_override) {
        this.name = name;
        this.TileLocation = location;
        this.PathfindingOverride = path_override;
    }
}