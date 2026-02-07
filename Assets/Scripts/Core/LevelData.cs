using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostSouls.Core
{
    [Serializable]
    public class LevelData
    {
        public int level_id;
        public string title;
        public string description;
        public GridData grid;
        public List<ObjectData> objects;
        public List<CharacterData> characters;
        public List<ObjectiveData> objectives;
        public int prompt_budget;
        public int prompt_max_length;
        public int par_score;
        public HintData hints;

        public static LevelData FromJson(string json)
        {
            return JsonUtility.FromJson<LevelData>(json);
        }
    }

    [Serializable]
    public class GridData
    {
        public int width;
        public int height;
        public List<TileData> tiles;
    }

    [Serializable]
    public class TileData
    {
        public int x;
        public int y;
        public string type; // floor, wall, exit, door, pressure_plate, pedestal

        public TileType GetTileType()
        {
            return type.ToLower() switch
            {
                "floor" => TileType.Floor,
                "wall" => TileType.Wall,
                "exit" => TileType.Exit,
                "door" => TileType.Door,
                "pressure_plate" => TileType.PressurePlate,
                "pedestal" => TileType.Pedestal,
                _ => TileType.Floor
            };
        }
    }

    [Serializable]
    public class Position
    {
        public int x;
        public int y;

        public Position() { }

        public Position(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }

        public static Position FromVector2Int(Vector2Int v)
        {
            return new Position(v.x, v.y);
        }

        public override bool Equals(object obj)
        {
            if (obj is Position other)
            {
                return x == other.x && y == other.y;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }
    }

    [Serializable]
    public class ObjectData
    {
        public string id;
        public string type; // key, gem, box, door, pedestal, pressure_plate
        public string display_name;
        public Position position;
        public ObjectProperties properties;

        public ObjectType GetObjectType()
        {
            return type.ToLower() switch
            {
                "key" => ObjectType.Key,
                "gem" => ObjectType.Gem,
                "box" => ObjectType.Box,
                "door" => ObjectType.Door,
                "pedestal" => ObjectType.Pedestal,
                "pressure_plate" => ObjectType.PressurePlate,
                _ => ObjectType.Key
            };
        }
    }

    [Serializable]
    public class ObjectProperties
    {
        // Common properties
        public string color;
        public string size;
        public string shape;

        // Key properties
        public string unlocks_door_id;

        // Door properties
        public string state; // open, closed, locked
        public string required_key_id;

        // Gem properties
        public string target_pedestal_id;

        // Pedestal properties
        public string accepts_gem_id;
        public bool activated;

        // Pressure plate properties
        public string linked_object_id;

        // Box properties
        public float weight;

        // Visual variant (1-based index, 0 = auto-assign)
        public int variant;
    }

    [Serializable]
    public class CharacterData
    {
        public string id;
        public string name;
        public Position position;
        public string profile_id;
        public CharacterProfileData profile; // Inline profile (optional, for simpler levels)
    }

    [Serializable]
    public class CharacterProfileData
    {
        public string profile_id;
        public string name;
        public string bio;
        public string model_prefab;
        public string comprehension; // simple, standard, clever
        public List<PerceptionQuirkData> perception_quirks;
        public List<PersonalityQuirkData> personality_quirks;
        public string direction_mode; // relative, absolute, inverted_lr, inverted_ns
        public HintResponsesData hint_responses;

        public ComprehensionLevel GetComprehensionLevel()
        {
            return comprehension?.ToLower() switch
            {
                "simple" => ComprehensionLevel.Simple,
                "standard" => ComprehensionLevel.Standard,
                "clever" => ComprehensionLevel.Clever,
                _ => ComprehensionLevel.Standard
            };
        }

        public DirectionMode GetDirectionMode()
        {
            return direction_mode?.ToLower() switch
            {
                "relative" => DirectionMode.Relative,
                "absolute" => DirectionMode.Absolute,
                "inverted_lr" => DirectionMode.InvertedLeftRight,
                "inverted_ns" => DirectionMode.InvertedNorthSouth,
                _ => DirectionMode.Absolute
            };
        }
    }

    [Serializable]
    public class PerceptionQuirkData
    {
        public string type; // colorblind, own_vocabulary, mirrored_view, size_distortion
        public PerceptionQuirkConfig config;
    }

    [Serializable]
    public class PerceptionQuirkConfig
    {
        // Colorblind
        public List<string[]> confused_pairs;
        public string replacement;

        // Own vocabulary
        public VocabularyMap vocabulary_map;

        // Mirrored view
        public string axis; // left_right, up_down

        // Size distortion
        public bool inversion;
    }

    [Serializable]
    public class VocabularyMap
    {
        // Unity's JsonUtility doesn't handle Dictionary well,
        // so we'll use a custom approach for vocabulary mapping
        // This will be handled specially in code
    }

    [Serializable]
    public class PersonalityQuirkData
    {
        public string type; // stubborn, unmotivated, polite, impatient, distrustful
        public PersonalityQuirkConfig config;
    }

    [Serializable]
    public class PersonalityQuirkConfig
    {
        // Stubborn
        public int refusal_count;
        public bool per_action_type;

        // Unmotivated
        public int max_steps_willing;
        public string refusal_response;

        // Polite
        public List<string> required_keywords;

        // Distrustful
        public int trust_threshold;
        public bool invert_before_trust;
        public string trust_response;
    }

    [Serializable]
    public class HintResponsesData
    {
        public string observe_idle;
        public string feeling_check;
    }

    [Serializable]
    public class ObjectiveData
    {
        public string type; // reach_exit, place_gem, unlock_door, etc.
        public string target_character;
        public string target_object;
        public Position target_position;
    }

    [Serializable]
    public class HintData
    {
        public bool bio_visible;
        public int observation_cost;
    }

    // Enums
    public enum TileType
    {
        Floor,
        Wall,
        Exit,
        Door,
        PressurePlate,
        Pedestal
    }

    public enum ObjectType
    {
        Key,
        Gem,
        Box,
        Door,
        Pedestal,
        PressurePlate
    }

    public enum ComprehensionLevel
    {
        Simple,    // 1 action max
        Standard,  // 2 actions max
        Clever     // 3+ actions, conditional logic
    }

    public enum DirectionMode
    {
        Relative,       // forward, backward, left, right (based on facing)
        Absolute,       // north, south, east, west
        InvertedLeftRight,
        InvertedNorthSouth
    }

    public enum Direction
    {
        North,
        South,
        East,
        West
    }

    public enum DoorState
    {
        Open,
        Closed,
        Locked
    }
}
