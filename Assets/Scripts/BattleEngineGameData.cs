// BattleGameData.cs - Centralized game content definitions
using System.Collections.Generic;
using EnhancedBattleSystem;

namespace BattleContent
{
    // Static database of all game content
    public static class GameDatabase
    {
        #region Skills
        public static class Skills
        {
            // Basic Combat Skills
            public static readonly SkillAction BasicAttack = new SkillAction
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                Description = "A simple strike",
                Damage = 20,
                BaseCost = 0,
                Range = 1,
                TargetType = TargetType.Single
            };
            
            public static readonly SkillAction PowerStrike = new SkillAction
            {
                Id = "power_strike",
                Name = "Power Strike",
                Description = "A powerful blow",
                Damage = 35,
                BaseCost = 5,
                Range = 1,
                TargetType = TargetType.Single
            };
            
            // Magic Skills
            public static readonly SkillAction Fireball = new SkillAction
            {
                Id = "fireball",
                Name = "Fireball",
                Description = "Launches a ball of fire",
                Damage = 30,
                BaseCost = 10,
                Range = 3,
                TargetType = TargetType.Single,
                AppliesStatus = "Burn",
                StatusDuration = 2
            };
            
            public static readonly SkillAction IceBlast = new SkillAction
            {
                Id = "ice_blast",
                Name = "Ice Blast",
                Description = "Freezes the target",
                Damage = 25,
                BaseCost = 12,
                Range = 2,
                TargetType = TargetType.Single,
                AppliesStatus = "Freeze",
                StatusDuration = 1
            };
            
            public static readonly SkillAction Lightning = new SkillAction
            {
                Id = "lightning",
                Name = "Lightning",
                Description = "Strikes all enemies",
                Damage = 20,
                BaseCost = 15,
                Range = 99,
                TargetType = TargetType.All
            };
            
            // Support Skills
            public static readonly SkillAction Heal = new SkillAction
            {
                Id = "heal",
                Name = "Heal",
                Description = "Restore HP to target",
                Damage = -30,
                BaseCost = 0,
                Range = 2,
                TargetType = TargetType.Single
            };
            
            public static readonly SkillAction GroupHeal = new SkillAction
            {
                Id = "group_heal",
                Name = "Group Heal",
                Description = "Heal all allies",
                Damage = -20,
                BaseCost = 20,
                Range = 99,
                TargetType = TargetType.All
            };
            
            public static readonly SkillAction Shield = new SkillAction
            {
                Id = "shield",
                Name = "Shield",
                Description = "Grants damage reduction",
                Damage = 0,
                BaseCost = 8,
                Range = 2,
                TargetType = TargetType.Single,
                AppliesStatus = "Shield",
                StatusDuration = 3
            };
            
            // Special Skills
            public static readonly SkillAction Flee = new SkillAction
            {
                Id = "flee",
                Name = "Flee",
                Description = "Attempt to escape",
                Damage = 0,
                BaseCost = 0,
                Range = 0,
                TargetType = TargetType.Self
            };
        }
        #endregion
        
        #region Movement Actions
        public static class Movement
        {
            public static readonly MoveAction Walk = new MoveAction
            {
                Id = "walk",
                Name = "Walk",
                Description = "Move 1 hex",
                BaseCost = 0,
                MoveRange = 1,
                TargetType = TargetType.Single
            };
            
            public static readonly MoveAction Sprint = new MoveAction
            {
                Id = "sprint",
                Name = "Sprint",
                Description = "Move 2 hexes quickly",
                BaseCost = 3,
                MoveRange = 2,
                TargetType = TargetType.Single
            };
            
            public static readonly MoveAction Dash = new MoveAction
            {
                Id = "dash",
                Name = "Dash",
                Description = "Move 3 hexes",
                BaseCost = 8,
                MoveRange = 3,
                TargetType = TargetType.Single
            };
            
            public static readonly MoveAction Teleport = new MoveAction
            {
                Id = "teleport",
                Name = "Teleport",
                Description = "Instantly move 4 hexes",
                BaseCost = 15,
                MoveRange = 4,
                TargetType = TargetType.Single
            };
        }
        #endregion
        
        #region Talk Actions
        public static class Talk
        {
            public static readonly TalkAction SmallTalk = new TalkAction
            {
                Id = "small_talk",
                Name = "Small Talk",
                Description = "Friendly conversation",
                BaseCost = 0,
                FriendshipChange = 1,
                ReputationChange = 0,
                Dialogue = "Nice weather we're having!",
                TargetType = TargetType.Single
            };
            
            public static readonly TalkAction Compliment = new TalkAction
            {
                Id = "compliment",
                Name = "Compliment",
                Description = "Praise the target",
                BaseCost = 0,
                FriendshipChange = 3,
                ReputationChange = 1,
                Dialogue = "You're doing great!",
                TargetType = TargetType.Single
            };
            
            public static readonly TalkAction Bribe = new TalkAction
            {
                Id = "bribe",
                Name = "Bribe",
                Description = "Offer money for favor",
                BaseCost = 20,
                FriendshipChange = 10,
                ReputationChange = -5,
                Dialogue = "How about we make a deal?",
                TargetType = TargetType.Single
            };
            
            public static readonly TalkAction Intimidate = new TalkAction
            {
                Id = "intimidate",
                Name = "Intimidate",
                Description = "Threaten the target",
                BaseCost = 0,
                FriendshipChange = -10,
                ReputationChange = 5,
                Dialogue = "You don't want to mess with me!",
                TargetType = TargetType.Single
            };
            
            public static readonly TalkAction Negotiate = new TalkAction
            {
                Id = "negotiate",
                Name = "Negotiate",
                Description = "Discuss terms",
                BaseCost = 5,
                FriendshipChange = 0,
                ReputationChange = 3,
                Dialogue = "Let's work something out.",
                TargetType = TargetType.Single
            };
        }
        #endregion
        
        #region Items
        public static class Items
        {
            public static readonly ItemAction HealthPotion = new ItemAction
            {
                Id = "health_potion",
                Name = "Health Potion",
                Description = "Restore 30 HP",
                BaseCost = 0,
                HealAmount = 30,
                UsesRemaining = 3,
                TargetType = TargetType.Single
            };
            
            public static readonly ItemAction MegaPotion = new ItemAction
            {
                Id = "mega_potion",
                Name = "Mega Potion",
                Description = "Restore 60 HP",
                BaseCost = 0,
                HealAmount = 60,
                UsesRemaining = 1,
                TargetType = TargetType.Single
            };
            
            public static readonly ItemAction PoisonVial = new ItemAction
            {
                Id = "poison_vial",
                Name = "Poison Vial",
                Description = "Inflict poison status",
                BaseCost = 5,
                DamageAmount = 5,
                AppliesStatus = "Poison",
                StatusDuration = 3,
                UsesRemaining = 2,
                TargetType = TargetType.Single
            };
            
            public static readonly ItemAction SmokeBomb = new ItemAction
            {
                Id = "smoke_bomb",
                Name = "Smoke Bomb",
                Description = "Create cover to escape",
                BaseCost = 0,
                AppliesStatus = "Hidden",
                StatusDuration = 1,
                UsesRemaining = 1,
                TargetType = TargetType.Self
            };
            
            public static readonly ItemAction Antidote = new ItemAction
            {
                Id = "antidote",
                Name = "Antidote",
                Description = "Cure poison",
                BaseCost = 0,
                RemovesStatus = "Poison",
                UsesRemaining = 2,
                TargetType = TargetType.Single
            };
        }
        #endregion
        
        #region Character Templates
        public static class Characters
        {
            public static Entity CreatePlayer(string name = "Hero")
            {
                return new Entity(name, EntityType.Player, 120, 50)
                {
                    Money = 50,
                    Reputation = 50
                };
            }
            
            public static Entity CreateKnight(string name = "Knight")
            {
                return new Entity(name, EntityType.Ally, 150, 40)
                {
                    Friendship = 60,
                    Money = 20
                };
            }
            
            public static Entity CreateMage(string name = "Mage")
            {
                return new Entity(name, EntityType.Ally, 80, 55)
                {
                    Friendship = 70,
                    Money = 30
                };
            }
            
            public static Entity CreateRogue(string name = "Rogue")
            {
                return new Entity(name, EntityType.Ally, 100, 70)
                {
                    Friendship = 40,
                    Money = 50
                };
            }
            
            public static Entity CreateMercenary(string name = "Mercenary")
            {
                return new Entity(name, EntityType.Ally, 110, 45)
                {
                    Friendship = 30,  // Low friendship = higher fees
                    Money = 40
                };
            }
            
            // Enemies
            public static Entity CreateGoblin(string name = "Goblin")
            {
                return new Entity(name, EntityType.Enemy, 60, 60);
            }
            
            public static Entity CreateOrc(string name = "Orc")
            {
                return new Entity(name, EntityType.Enemy, 100, 35);
            }
            
            public static Entity CreateBandit(string name = "Bandit")
            {
                return new Entity(name, EntityType.Enemy, 80, 50);
            }
            
            public static Entity CreateDarkMage(string name = "Dark Mage")
            {
                return new Entity(name, EntityType.Enemy, 70, 45);
            }
            
            // NPCs
            public static Entity CreateMerchant(string name = "Merchant")
            {
                return new Entity(name, EntityType.NPC, 50, 25)
                {
                    Money = 200,
                    Friendship = 50
                };
            }
            
            public static Entity CreateNoble(string name = "Noble")
            {
                return new Entity(name, EntityType.NPC, 40, 20)
                {
                    Money = 500,
                    Friendship = 30,
                    Reputation = 80
                };
            }
        }
        #endregion
        
        #region Skill Sets (which skills each character type has)
        public static class SkillSets
        {
            public static readonly List<BattleAction> PlayerSkills = new List<BattleAction>
            {
                Skills.BasicAttack,
                Skills.Fireball,
                Skills.Heal,
                Skills.Flee
            };
            
            public static readonly List<BattleAction> KnightSkills = new List<BattleAction>
            {
                Skills.BasicAttack,
                Skills.PowerStrike,
                Skills.Shield
            };
            
            public static readonly List<BattleAction> MageSkills = new List<BattleAction>
            {
                Skills.Fireball,
                Skills.IceBlast,
                Skills.Lightning,
                Skills.Heal
            };
            
            public static readonly List<BattleAction> RogueSkills = new List<BattleAction>
            {
                Skills.BasicAttack,
                Skills.PowerStrike
            };
            
            public static readonly List<BattleAction> GoblinSkills = new List<BattleAction>
            {
                Skills.BasicAttack
            };
            
            public static readonly List<BattleAction> OrcSkills = new List<BattleAction>
            {
                Skills.BasicAttack,
                Skills.PowerStrike
            };
            
            public static readonly List<BattleAction> DarkMageSkills = new List<BattleAction>
            {
                Skills.Fireball,
                Skills.IceBlast
            };
        }
        #endregion
        
        #region Battle Configurations
        public static class BattleConfigs
        {
            public static class TestBattle
            {
                public static List<(Entity entity, Godot.Vector2I position)> GetSetup()
                {
                    var setup = new List<(Entity, Godot.Vector2I)>();
                    
                    // Player team
                    setup.Add((Characters.CreatePlayer(), new Godot.Vector2I(0, 0)));
                    setup.Add((Characters.CreateKnight("Sir Lancelot"), new Godot.Vector2I(1, 0)));
                    setup.Add((Characters.CreateMage("Merlin"), new Godot.Vector2I(0, 1)));
                    
                    // Enemies
                    setup.Add((Characters.CreateGoblin("Goblin A"), new Godot.Vector2I(4, 0)));
                    setup.Add((Characters.CreateGoblin("Goblin B"), new Godot.Vector2I(4, 1)));
                    setup.Add((Characters.CreateOrc("Orc Chief"), new Godot.Vector2I(5, 0)));
                    
                    // NPC
                    setup.Add((Characters.CreateMerchant("Trader Joe"), new Godot.Vector2I(2, 3)));
                    
                    return setup;
                }
            }
            
            public static class ArenaBattle
            {
                public static List<(Entity entity, Godot.Vector2I position)> GetSetup()
                {
                    var setup = new List<(Entity, Godot.Vector2I)>();
                    
                    // Player team
                    setup.Add((Characters.CreatePlayer("Champion"), new Godot.Vector2I(2, 2)));
                    setup.Add((Characters.CreateMercenary("Hired Blade"), new Godot.Vector2I(1, 2)));
                    
                    // Enemies - surrounding formation
                    setup.Add((Characters.CreateBandit("Bandit Leader"), new Godot.Vector2I(4, 2)));
                    setup.Add((Characters.CreateBandit("Bandit Thug"), new Godot.Vector2I(3, 1)));
                    setup.Add((Characters.CreateBandit("Bandit Grunt"), new Godot.Vector2I(3, 3)));
                    
                    return setup;
                }
            }
        }
        #endregion
    }
    
    // Helper class to load actions into dictionary
    public static class ActionLoader
    {
        public static Dictionary<string, BattleAction> LoadAllActions()
        {
            var actions = new Dictionary<string, BattleAction>();
            
            // Load Skills
            actions["basic_attack"] = GameDatabase.Skills.BasicAttack;
            actions["power_strike"] = GameDatabase.Skills.PowerStrike;
            actions["fireball"] = GameDatabase.Skills.Fireball;
            actions["ice_blast"] = GameDatabase.Skills.IceBlast;
            actions["lightning"] = GameDatabase.Skills.Lightning;
            actions["heal"] = GameDatabase.Skills.Heal;
            actions["group_heal"] = GameDatabase.Skills.GroupHeal;
            actions["shield"] = GameDatabase.Skills.Shield;
            actions["flee"] = GameDatabase.Skills.Flee;
            
            // Load Movement
            actions["walk"] = GameDatabase.Movement.Walk;
            actions["sprint"] = GameDatabase.Movement.Sprint;
            actions["dash"] = GameDatabase.Movement.Dash;
            actions["teleport"] = GameDatabase.Movement.Teleport;
            
            // Load Talk
            actions["small_talk"] = GameDatabase.Talk.SmallTalk;
            actions["compliment"] = GameDatabase.Talk.Compliment;
            actions["bribe"] = GameDatabase.Talk.Bribe;
            actions["intimidate"] = GameDatabase.Talk.Intimidate;
            actions["negotiate"] = GameDatabase.Talk.Negotiate;
            
            // Load Items
            actions["health_potion"] = GameDatabase.Items.HealthPotion;
            actions["mega_potion"] = GameDatabase.Items.MegaPotion;
            actions["poison_vial"] = GameDatabase.Items.PoisonVial;
            actions["smoke_bomb"] = GameDatabase.Items.SmokeBomb;
            actions["antidote"] = GameDatabase.Items.Antidote;
            
            return actions;
        }
    }
}