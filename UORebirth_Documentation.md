# UORebirth Codebase Documentation

## Overview

UORebirth is a complete Ultima Online server emulator based on the RunUO framework, designed to recreate the authentic Pre-T2A (The Second Age) UO experience from 1997-1998. This codebase represents the official release of UOGamers: Rebirth scripts and core source code from September 20, 2005.

## Project Background

- **Original Era**: 1997-1998 Ultima Online (Pre-T2A)
- **Development Period**: 2004-2006
- **Original Team**: UOGamers/RunUO team
- **Target Experience**: Authentic early UO gameplay with period-accurate mechanics
- **Core Framework**: RunUO 1.0.1 (updated for modern 2.1+/SVN compatibility)

## Architecture Overview

### Core Components

#### 1. Server Core (`RunUO.exe`)
- Modified RunUO core with Pre-T2A compatibility patches
- ASCII text conversion for authentic period communication
- Core changes documented in `Rebirth-Core-svn728.diff`

#### 2. Script System (`Scripts/`)
- C#-based scripting system
- Modular architecture with clear separation of concerns
- Event-driven programming model
- Comprehensive skill, spell, and item systems

#### 3. World Data (`Data/`)
- XML-based world configuration
- Spawn definitions and region management
- Static world data and object placement

## Directory Structure

### Root Level
```
UORebirth/
├── RunUO.exe                 # Modified server executable
├── README.txt               # Original project documentation
├── Rebirth-Core-svn728.diff # Core modification patches
├── Scripts/                 # Main script codebase
├── Data/                    # World data and configuration
├── Saves/                   # World save data
├── Backups/                 # Automatic backup system
└── Logs/                    # Server logs
```

### Scripts Directory Structure

#### Core Systems (`Scripts/`)
- **Accounting/** - Account management and security
- **Commands/** - Administrative and player commands
- **Engines/** - Core game systems and mechanics
- **Gumps/** - User interface components
- **Items/** - All game items and equipment
- **Mobiles/** - NPCs, monsters, and player characters
- **Multis/** - Houses, boats, and multi-tile structures
- **Regions/** - World regions and area management
- **Skills/** - Skill system implementations
- **Spells/** - Magic system and spell implementations
- **Targets/** - Targeting system components
- **Special/** - Specialized game features

#### Engines Subsystem (`Scripts/Engines/`)
- **AI/** - Artificial intelligence for NPCs and monsters
- **Chat/** - Communication systems
- **Craft/** - Crafting and skill-based item creation
- **Harvest/** - Resource gathering systems
- **Help/** - Help and support systems
- **MyRunUO/** - Web integration features
- **Pathing/** - Movement and pathfinding
- **Spawner/** - Dynamic world spawning
- **VeteranRewards/** - Player reward systems

## Core Systems Documentation

### 1. Player System (`Scripts/Mobiles/PlayerMobile.cs`)

#### Key Features
- **Notoriety System**: Pre-T2A karma/fame combined system
- **Bounty System**: Player bounty tracking and rewards
- **Skill Atrophy**: No skill locks, skills can decrease
- **Movement Control**: Authentic period movement mechanics
- **Speech System**: Hidden speech and communication crystals
- **Death System**: Resurrect now option with stat penalties

#### Player Flags
```csharp
[Flags]
public enum PlayerFlag
{
    None = 0x00000000,
    Glassblowing = 0x00000001,
    Masonry = 0x00000002,
    SandMining = 0x00000004,
    StoneMining = 0x00000008,
    ToggleMiningStone = 0x00000010,
    KarmaLocked = 0x00000020,
    AutoRenewInsurance = 0x00000040,
    UseOwnFilter = 0x00000080,
    PublicMyRunUO = 0x00000100,
    PagingSquelched = 0x00000200
}
```

#### Key Properties
- `Bounty` - Player bounty amount
- `NextNotoUp` - Notoriety update timing
- `NpcGuild` - Guild membership
- `Flags` - Player feature flags
- `Learning` - Current skill learning focus

### 2. Spell System (`Scripts/Spells/`)

#### Architecture
- **Base Spell Class**: Abstract spell implementation
- **Circle-based Organization**: 8 spell circles (First through Eighth)
- **Pre-UO:R Damage System**: Authentic period damage calculations
- **Reagent System**: Material requirements for spell casting
- **Resistance System**: Magic resistance mechanics

#### Spell Circles
```csharp
public enum SpellCircle
{
    First = 1,
    Second = 2,
    Third = 3,
    Fourth = 4,
    Fifth = 5,
    Sixth = 6,
    Seventh = 7,
    Eighth = 8
}
```

#### Damage Calculation
```csharp
public static int GetPreUORDamage(SpellCircle Circle)
{
    switch (Circle)
    {
        case SpellCircle.First: return Utility.Dice(1,3,3);
        case SpellCircle.Second: return Utility.Dice(1,8,4);
        case SpellCircle.Third: return Utility.Dice(4,4,4);
        case SpellCircle.Fourth: return Utility.Dice(3,8,5);
        case SpellCircle.Fifth: return Utility.Dice(5,8,6);
        case SpellCircle.Sixth: return Utility.Dice(6,8,8);
        case SpellCircle.Seventh: return Utility.Dice(7,8,10);
        case SpellCircle.Eighth: return Utility.Dice(7,8,10);
    }
    return 1;
}
```

### 3. Crafting System (`Scripts/Engines/Craft/`)

#### Core Components
- **CraftSystem**: Abstract base class for all crafting
- **CraftSystemItem**: Individual craftable items
- **CraftMenu**: User interface for crafting
- **Resource Management**: Material consumption and requirements

#### Supported Crafts
- **Alchemy** - Potions and magical items
- **Blacksmithy** - Weapons and armor
- **Carpentry** - Wooden items and furniture
- **Cartography** - Maps and navigation
- **Fletching** - Bows and arrows
- **Inscribe** - Scrolls and magical writing
- **Tailoring** - Clothing and cloth items
- **Tinkering** - Mechanical devices

#### Crafting Process
1. **Menu Selection**: Player chooses craft type
2. **Item Selection**: Player selects specific item
3. **Resource Check**: System verifies required materials
4. **Skill Check**: Success based on player skill level
5. **Item Creation**: Successful crafting creates item

### 4. Item System (`Scripts/Items/`)

#### Base Item Class (`BaseItem.cs`)
- **Single Click Names**: Authentic period item identification
- **Pluralization**: Automatic item name pluralization
- **Loot Types**: Blessed/cursed item identification
- **ASCII Communication**: Period-accurate text display

#### Item Categories
- **Armor/** - All armor types and sets
- **Weapons/** - Melee, ranged, and magical weapons
- **Resources/** - Raw materials and crafting components
- **Containers/** - Bags, chests, and storage items
- **Construction/** - Building materials and furniture
- **Skill Items/** - Tools and skill-specific items
- **Special/** - Holiday items and unique objects

#### Item Naming System
```csharp
public virtual void AppendClickName(StringBuilder sb)
{
    // Handles pluralization and article insertion
    // Example: "an indestructible halberd of vanquishing"
}
```

### 5. Mobile System (`Scripts/Mobiles/`)

#### Mobile Types
- **PlayerMobile** - Player character implementation
- **Animals/** - Passive and mountable creatures
- **Monsters/** - Hostile NPCs and creatures
- **Humans/** - NPCs, vendors, and town residents
- **Guards/** - Town protection and law enforcement

#### AI System (`Scripts/Engines/AI/`)
- **Creature AI** - Monster behavior patterns
- **Targeting** - Combat and interaction targeting
- **Team AI** - Group behavior coordination
- **Pathfinding** - Movement and navigation

### 6. World Management

#### Regions (`Data/Regions.xml`)
- **Town Regions**: Protected areas with guards
- **Dungeon Regions**: Dangerous areas
- **Wilderness**: Unprotected outdoor areas
- **Special Areas**: Unique locations and dungeons

#### Spawning (`Data/WorldSpawn.xml`)
- **Static Spawns**: Fixed NPC and monster locations
- **Dynamic Spawning**: Time-based creature respawning
- **Spawn Groups**: Coordinated monster groups
- **Vendor Placement**: Town merchant locations

#### World Maps
- **Felucca**: Main world map
- **Trammel**: Peaceful alternate world
- **Ilshenar**: Expansion areas
- **Malas**: Additional content areas

### 7. Skill System (`Scripts/Skills/`)

#### Skill Implementation
- **46 Total Skills**: Complete period skill set
- **Skill Atrophy**: Skills can decrease without locks
- **Gain Formulas**: Authentic skill progression
- **Global Usage Tracking**: Rare skills gain faster

#### Key Skills
- **Combat Skills**: Swords, Maces, Fencing, Archery
- **Magic Skills**: Magery, Magic Resistance, Meditation
- **Crafting Skills**: Blacksmithy, Tailoring, Alchemy
- **Utility Skills**: Stealth, Tracking, Animal Taming
- **Social Skills**: Provocation, Peacemaking, Discordance

### 8. Communication Systems

#### Chat Engine (`Scripts/Engines/Chat/`)
- **Local Chat**: Area-based communication
- **Hidden Speech**: Speaking while hidden
- **Communication Crystals**: Long-distance messaging
- **Party System**: Group communication

#### Speech Mechanics
```csharp
public override void OnSaid(SpeechEventArgs e)
{
    // Handles speech processing
    // Includes hidden speech mechanics
    // Manages communication crystals
}
```

### 9. Command System (`Scripts/Commands/`)

#### Administrative Commands
- **Add** - Spawn items and mobiles
- **Properties** - Modify object properties
- **Spawn** - Create spawn points
- **Decorate** - World decoration tools
- **Batch** - Bulk operations

#### Player Commands
- **Skills** - Skill management
- **Help** - Assistance system
- **Logging** - Activity tracking

#### Abstracted Commands (`Scripts/Commands/Abstracted/`)
- **Global Commands** - World-wide operations
- **Area Commands** - Regional operations
- **Target Commands** - Object-specific operations

### 10. Data Management

#### Save System (`Saves/`)
- **Accounts/** - Player account data
- **Mobiles/** - Character and NPC data
- **Items/** - World item persistence
- **Guilds/** - Guild information

#### Configuration Files
- **WorldSpawn.xml** - World spawn definitions
- **Regions.xml** - Region configurations
- **Objects.xml** - Static object placement
- **Names.xml** - NPC name generation

## Pre-T2A Ruleset Features

### 1. Notoriety System
- Combined karma and fame system
- Easy to become "red" (murderer)
- Permanent grey status from stealing
- No modern karma/fame separation

### 2. Bounty System
- Player-funded bounty rewards
- Head collection mechanics
- Bounty board displays
- Automatic bounty decay

### 3. Chaos/Order System
- Guild virtue declarations
- Virtue shield mechanics
- Town combat permissions
- Pre-faction system

### 4. Housing System
- No lockdowns or secures
- Key-based ownership
- No item decay in houses
- Simple security model

### 5. Movement Mechanics
- Horse lag simulation
- Authentic period movement speeds
- Turn-based lag effects
- Combat movement restrictions

### 6. Skill Mechanics
- No skill locks
- Skill atrophy system
- Difficult skill progression
- Global usage balancing

## Technical Implementation Details

### 1. Packet System
- ASCII text conversion for authenticity
- Unicode to ASCII translation
- Period-accurate communication protocols

### 2. Memory Management
- Efficient object pooling
- Garbage collection optimization
- Memory leak prevention

### 3. Performance Optimization
- Cached packet generation
- Efficient pathfinding algorithms
- Optimized spawn management

### 4. Security Features
- Account protection systems
- Anti-cheat mechanisms
- Connection limiting
- Firewall integration

## Development Guidelines

### 1. Code Organization
- Clear namespace separation
- Consistent naming conventions
- Modular design principles
- Event-driven architecture

### 2. Authenticity Requirements
- Period-accurate mechanics
- Original UO data references
- Historical accuracy validation
- Community feedback integration

### 3. Performance Considerations
- Efficient resource usage
- Scalable architecture
- Memory optimization
- Network efficiency

### 4. Maintenance Procedures
- Regular backup systems
- Log monitoring
- Performance tracking
- Community feedback integration

## Deployment and Configuration

### 1. Server Setup
- RunUO core installation
- Script compilation
- World data loading
- Configuration optimization

### 2. World Management
- Spawn configuration
- Region setup
- NPC placement
- Item distribution

### 3. Backup Systems
- Automatic backup scheduling
- Data integrity verification
- Recovery procedures
- Archive management

### 4. Monitoring
- Performance metrics
- Player activity tracking
- Error logging
- System health monitoring

## Community and Support

### 1. Original Development
- UOGamers team contribution
- Community feedback integration
- Historical accuracy validation
- Period research methodology

### 2. Documentation Sources
- UODemo data extraction
- Historical patch notes
- Community memory preservation
- Internet archive research

### 3. Legacy Preservation
- Codebase archival
- Historical accuracy maintenance
- Community knowledge preservation
- Educational value retention

## Conclusion

The UORebirth codebase represents a comprehensive recreation of the early Ultima Online experience, preserving the authentic mechanics and feel of the 1997-1998 era. The modular architecture, comprehensive documentation, and period-accurate implementation make it an invaluable resource for understanding early MMORPG design and preserving gaming history.

This documentation provides a foundation for understanding, maintaining, and potentially extending the UORebirth system while preserving its historical authenticity and technical excellence. 