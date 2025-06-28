# UORebirth

A faithful recreation and preservation of the classic UOGamers: Rebirth Ultima Online shard, featuring an authentic Pre-T2A (The Second Age) ruleset from the golden era of 1997-1998.

## Overview

UORebirth is a comprehensive Ultima Online server emulation project built on the RunUO 2.1 framework. This project preserves the authentic gameplay experience of early Ultima Online, specifically targeting the ruleset and mechanics that existed in late 1997 and early 1998 - before many of the major expansions changed the fundamental nature of the game.

## Features

### Core Gameplay Systems
- **Pre-T2A Ruleset**: Authentic recreation of Ultima Online as it existed in its first year
- **Classic Notoriety System**: Simple good vs. evil alignment system
- **Skill Atrophy**: No skill locks - skills can drop as others are gained, creating challenging character development
- **Original Combat Mechanics**: Authentic weapon modifiers, spell systems, and damage calculations
- **Chaos/Order System**: The original faction system predating modern factions

### Unique Pre-T2A Features
- **Bounty System**: Classic head-hunting system where murderers' heads can be collected for rewards
- **Communication Crystals**: Long-distance chat system using gem-powered crystal networks
- **Classic Housing**: No lockdowns or secures - pure key-based security system
- **Talking While Hidden**: Recreated client behavior allowing speech without revealing position
- **Original Item Naming**: Proper single-click names without context menus
- **Colored Armor**: NPC vendors sell randomly colored armor sets
- **Horse Lag Simulation**: Authentic movement penalties to balance mounted combat

### Technical Architecture
- **RunUO 2.1 Core**: Modern server core with backward compatibility
- **C# 2.0 Compliance**: Adheres to older language standards for authenticity
- **ASCII Message Conversion**: Automatic Unicode to ASCII conversion for period-accurate text display
- **Original Skill Formulas**: Custom implementations based on UODemo data extraction
- **Dynamic Skill Gain**: Global usage statistics affect individual skill gain rates

## Project Structure

```
UORebirth/
├── Scripts/              # Game mechanics and content scripts
│   ├── Accounting/       # Player account management
│   ├── Commands/         # Administrative and player commands
│   ├── Engines/          # Core game systems (AI, crafting, etc.)
│   ├── Items/           # Item definitions and behaviors
│   ├── Mobiles/         # NPC and creature definitions
│   ├── Skills/          # Skill system implementations
│   └── Spells/          # Magic system
├── Data/                # Configuration files and world data
├── Saves/               # World save files
└── Backups/             # Automated backup system
```

## Getting Started

### Prerequisites
- RunUO 2.1 or compatible server core
- .NET Framework 2.0 or later
- Ultima Online client (7.0+ supported)

### Installation
1. Clone or download this repository
2. Apply the core modifications found in `Rebirth-Core-svn728.diff`
3. Compile the scripts using the included project files
4. Configure your server settings in the Data directory
5. Start the server and create admin accounts as needed

### World Data
The project includes a complete world save from the original UOGamers: Rebirth shard (September 20, 2005). All player accounts have been removed for privacy, but characters, items, and structures remain intact for historical preservation.

## PlayerBots System

**🎮 Dynamic AI Population System**

UORebirth features a comprehensive **PlayerBots System** that creates intelligent AI players to populate the world and enhance gameplay through dynamic interactions and events.

### Current Features
The PlayerBots system brings life to the world through:
- **Dynamic Population**: AI bots automatically spawn and maintain population levels across different regions
- **Enhanced Movement**: Bots use running movement with extended wander ranges for realistic exploration
- **War Scenes**: Dynamic faction conflicts that spawn automatically in appropriate regions
- **Persona Diversity**: Three distinct bot types (Adventurers, Crafters, Player Killers) with unique behaviors
- **Regional Adaptation**: Bot spawning adapts to region safety levels and characteristics

### Implementation Status
- ✅ **Core Framework**: Complete PlayerBot classes with enhanced AI system
- ✅ **Director System**: Central management for bot populations and behaviors  
- ✅ **War Scene System**: Dynamic faction wars with intelligent participant selection
- ✅ **Regional Management**: Smart population control based on region capacity and safety
- ✅ **Enhanced Wandering**: Running movement with greatly extended ranges (up to 500 tiles)
- ✅ **Persona System**: Adventurer, Crafter, and PlayerKiller profiles with appropriate behaviors
- ✅ **Configuration System**: Comprehensive config files and admin command interface

### Key Components
- **PlayerBotDirector**: Central singleton managing all bot lifecycle, population control, and event coordination
- **War Scene System**: Generates dynamic faction conflicts with 8-16 participants in appropriate regions
- **Regional Population Control**: Intelligent spawning based on region safety levels and player presence  
- **Enhanced AI**: Bots use running movement and extended wander ranges for realistic world exploration
- **Admin Interface**: Complete command system and status GUI for monitoring and management

### Technical Implementation
The system maintains full compatibility with:
- RunUO 2.1 Core architecture
- C# 2.0 language standards (no modern language features)
- Pre-T2A authenticity and game balance
- Single-threaded execution model

This system creates a living, dynamic world where AI characters contribute to the authentic UO experience while maintaining the classic feel of the Pre-T2A era.

## Contributing

This project maintains historical accuracy as its primary goal. Contributions should:
- Adhere to C# 2.0 language standards (no `var`, no ternary operators)
- Maintain RunUO 2.1 compatibility
- Preserve Pre-T2A authenticity
- Include appropriate documentation and testing

## Historical Context

UORebirth preserves the work of the original UOGamers: Rebirth development team (2004-2006). The original shard was created to capture the essence of Ultima Online during its inaugural year, when the game was still finding its identity and the community was discovering the boundaries of this virtual world.

This project serves as both a playable server and a historical archive, preserving countless hours of development work, world building, and community memories from one of UO's most authentic recreation projects.

## License and Credits

- Original UOGamers: Rebirth scripts and world data: Property of UOGamers/RunUO team
- RunUO core and framework: See [RunUO License](http://www.runuo.com)
- Original documentation and credits: See `README-ORIGINAL.txt`
- Code marked as "Zippy": Released to public domain

## Support

This is a historical preservation project. While the code is provided as-is for educational and nostalgic purposes, active support is not available. Users should have strong familiarity with RunUO, C#, and Ultima Online server administration.

## Documentation

For detailed historical information, original feature descriptions, and technical notes, please refer to:
- `README-ORIGINAL.txt` - Complete original documentation
- `Rebirth-Core-svn728.diff` - Required core modifications
- `Specs/` - Additional technical specifications

---

*"Countless hours of programming, testing, world building, and playing time went into the content of this project. It is our sincere hope that this package will bring happiness and fun to some nostalgic players and administrators somewhere."*

*- Original UOGamers: Rebirth Team* 