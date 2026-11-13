using Paradox.Core;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>Room-aligned foreground copies for furniture the player can walk behind.</summary>
    internal static class MuseumForeground
    {
        private readonly struct Entry
        {
            public readonly string Room, Resource;
            public readonly float FootY;
            public readonly Vector2[] Pots;
            public Entry(string room, string resource, float footY, params Vector2[] pots)
            { Room = room; Resource = resource; FootY = footY; Pots = pots; }
        }

        private static readonly Entry[] Entries =
        {
            new Entry("00_Central_Hall", "00_Central_Hall__Statue", 193f),
            new Entry("01_Art_Gallery", "01_Art_Gallery__LeftCase", 266f),
            new Entry("01_Art_Gallery", "01_Art_Gallery__MainCase", 270f),
            new Entry("01_Art_Gallery", "01_Art_Gallery__LeftPost", 270f, new Vector2(184f, 265f)),
            new Entry("01_Art_Gallery", "01_Art_Gallery__RightPost", 270f, new Vector2(325f, 265f)),
            new Entry("01_Art_Gallery", "01_Art_Gallery__RightCase", 266f),
            new Entry("01_Art_Gallery", "01_Art_Gallery__Bench", 322f),
            new Entry("02_Ceramics_Gallery", "02_Ceramics_Gallery__LeftCase", 267f),
            new Entry("02_Ceramics_Gallery", "02_Ceramics_Gallery__MainCase", 270f),
            new Entry("02_Ceramics_Gallery", "02_Ceramics_Gallery__RightCase", 267f),
            new Entry("02_Ceramics_Gallery", "02_Ceramics_Gallery__Bench", 323f),
            new Entry("03_Natural_History", "03_Natural_History__LeftBench", 156f),
            new Entry("03_Natural_History", "03_Natural_History__RightBench", 156f),
            new Entry("03_Natural_History", "03_Natural_History__Fossil", 299f),
            new Entry("03_Natural_History", "03_Natural_History__FossilLeftPost", 310f, new Vector2(65f, 306f)),
            new Entry("03_Natural_History", "03_Natural_History__FossilRightPost", 310f, new Vector2(449f, 306f)),
            new Entry("04_History_Gallery", "04_History_Gallery__ShipCase", 287f),
            new Entry("04_History_Gallery", "04_History_Gallery__Bench", 325f),
            new Entry("05_Entrance_Reception", "05_Entrance_Reception__Desk", 256f),
            new Entry("06_Upper_Corridor", "06_Upper_Corridor__LeftBench", 299f),
            new Entry("06_Upper_Corridor", "06_Upper_Corridor__RightBench", 299f),
            new Entry("07_Curators_Office", "07_Curators_Office__Desk", 256f),
            new Entry("08_Storage", "08_Storage__CrateLeftTop", 210f),
            new Entry("08_Storage", "08_Storage__CrateLeftSmall", 211f),
            new Entry("08_Storage", "08_Storage__CrateRightTop", 210f),
            new Entry("08_Storage", "08_Storage__CrateLeftBottom", 269f),
            new Entry("08_Storage", "08_Storage__CrateRightBottom", 269f),
            new Entry("08_Storage", "08_Storage__Desk", 261f),
            new Entry("09_Conservation_Room", "09_Conservation_Room__LeftShelf", 260f),
            new Entry("09_Conservation_Room", "09_Conservation_Room__RightShelf", 260f),
            new Entry("09_Conservation_Room", "09_Conservation_Room__Desk", 263f),
            new Entry("10_Reading_Room", "10_Reading_Room__LeftDesk", 230f),
            new Entry("10_Reading_Room", "10_Reading_Room__RightDesk", 230f),
            new Entry("10_Reading_Room", "10_Reading_Room__Bench", 326f),
            new Entry("00_Central_Hall", "00_Central_Hall__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("00_Central_Hall", "00_Central_Hall__PlantsRight", 350f, new Vector2(460f, 345f)),
            new Entry("00_Central_Hall", "00_Central_Hall__PlantsMidLeft", 190f, new Vector2(192f, 185f)),
            new Entry("00_Central_Hall", "00_Central_Hall__PlantsMidRight", 190f, new Vector2(319f, 185f)),
            new Entry("01_Art_Gallery", "01_Art_Gallery__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("01_Art_Gallery", "01_Art_Gallery__PlantsRight", 350f, new Vector2(460f, 345f)),
            new Entry("02_Ceramics_Gallery", "02_Ceramics_Gallery__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("02_Ceramics_Gallery", "02_Ceramics_Gallery__PlantsRight", 350f, new Vector2(460f, 345f)),
            new Entry("03_Natural_History", "03_Natural_History__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("03_Natural_History", "03_Natural_History__PlantsRight", 350f, new Vector2(460f, 345f)),
            new Entry("04_History_Gallery", "04_History_Gallery__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("04_History_Gallery", "04_History_Gallery__PlantsRight", 350f, new Vector2(460f, 345f)),
            new Entry("05_Entrance_Reception", "05_Entrance_Reception__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("05_Entrance_Reception", "05_Entrance_Reception__PlantsLeftBack", 338f, new Vector2(63f, 333f)),
            new Entry("05_Entrance_Reception", "05_Entrance_Reception__PlantsRight", 350f, new Vector2(460f, 345f)),
            new Entry("05_Entrance_Reception", "05_Entrance_Reception__PlantsRightBack", 338f, new Vector2(448f, 333f)),
            new Entry("06_Upper_Corridor", "06_Upper_Corridor__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("06_Upper_Corridor", "06_Upper_Corridor__PlantsRight", 350f, new Vector2(460f, 345f)),
            new Entry("06_Upper_Corridor", "06_Upper_Corridor__PlantsMidLeft", 247f, new Vector2(69f, 242f)),
            new Entry("06_Upper_Corridor", "06_Upper_Corridor__PlantsMidRight", 247f, new Vector2(442f, 242f)),
            new Entry("07_Curators_Office", "07_Curators_Office__PlantsLeft", 350f, new Vector2(47f, 345f), new Vector2(67f, 342f)),
            new Entry("07_Curators_Office", "07_Curators_Office__PlantsRight", 350f, new Vector2(460f, 345f), new Vector2(446f, 342f)),
            new Entry("08_Storage", "08_Storage__PlantsLeft", 350f, new Vector2(47f, 345f)),
            new Entry("08_Storage", "08_Storage__PlantsRight", 350f, new Vector2(460f, 345f), new Vector2(455f, 344f)),
            new Entry("09_Conservation_Room", "09_Conservation_Room__PlantsLeft", 350f, new Vector2(47f, 345f), new Vector2(67f, 342f)),
            new Entry("09_Conservation_Room", "09_Conservation_Room__PlantsRight", 350f, new Vector2(460f, 345f), new Vector2(446f, 342f)),
            new Entry("10_Reading_Room", "10_Reading_Room__PlantsLeft", 350f, new Vector2(47f, 345f), new Vector2(65f, 338f)),
            new Entry("10_Reading_Room", "10_Reading_Room__PlantsRight", 350f, new Vector2(460f, 345f), new Vector2(448f, 338f)),
        };

        public static void Install(MuseumRoom[] rooms)
        {
            if (rooms == null) return;
            foreach (MuseumRoom room in rooms)
            {
                if (room == null || room.transform.Find("MuseumForeground") != null) continue;
                string expectedRoomName = room.gameObject.name;
                Transform art = room.transform.Find("Art");
                SpriteRenderer background = art != null ? art.GetComponent<SpriteRenderer>() : null;
                Transform parent = null;
                foreach (Entry entry in Entries)
                {
                    if (expectedRoomName != "Room_" + entry.Room) continue;
                    Sprite sprite = Resources.Load<Sprite>("MuseumOverlays/" + entry.Resource);
                    if (sprite == null) { Debug.LogWarning("Missing museum foreground: " + entry.Resource); continue; }
                    if (parent == null)
                    {
                        parent = new GameObject("MuseumForeground").transform;
                        parent.SetParent(room.transform, false);
                    }
                    var child = new GameObject(entry.Resource);
                    child.transform.SetParent(parent, false);
                    var renderer = child.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    if (background != null) renderer.sortingLayerID = background.sortingLayerID;
                    renderer.sortingOrder = YDepthSort.OrderFor((192f - entry.FootY) / 32f, 50f);
                    if (entry.Resource == "10_Reading_Room__Bench")
                    {
                        var benchCenter = child.AddComponent<BoxCollider2D>();
                        benchCenter.offset = new Vector2((258f - 256f) / 32f, (192f - 323f) / 32f);
                        benchCenter.size = new Vector2(52f / 32f, 17f / 32f);
                    }
                    foreach (Vector2 pot in entry.Pots)
                    {
                        var collider = child.AddComponent<BoxCollider2D>();
                        collider.offset = new Vector2((pot.x - 256f) / 32f, (192f - pot.y) / 32f);
                        collider.size = new Vector2(14f / 32f, 8f / 32f);
                    }
                }
            }
        }
    }
}
