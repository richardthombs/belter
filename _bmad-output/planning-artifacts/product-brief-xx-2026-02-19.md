---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments: ['_bmad-output/brainstorming/brainstorming-session-2026-02-19.md']
date: 2026-02-19
author: Richard
---

# Product Brief: Belter Life

<!-- Content will be appended sequentially through collaborative workflow steps -->

## Executive Summary

*Belter Life* is a 2D massively multiplayer space game set in a procedurally generated, ever-expanding asteroid belt. Players mine resources, survey the frontier, build infrastructure, trade on a player-driven economy, and form or join organisations — at whatever depth and pace suits them. The game is designed to be equally rewarding for a solo player with an hour to spare and an organisation with long-term territorial ambitions, with built-in systems that prevent any faction from dominating the experience of others.

---

## Core Vision

### Problem Statement

Existing space MMOs force a choice: either embrace overwhelming complexity and mandatory organisation membership (Eve Online), or accept shallow arcade mechanics with no persistence or depth. There is no accessible, physics-driven space MMO that a casual player can enjoy independently while still offering the depth that dedicated players and organisations crave.

### Problem Impact

Solo and casual players are excluded from the space MMO genre unless they commit to a level of time and social obligation most cannot sustain. Organisations become gatekeepers of content rather than optional communities.

### Why Existing Solutions Fall Short

Eve Online's fully player-driven economy, combined with unchecked organisational power, creates an environment where new and solo players are progressively squeezed out of meaningful content. No systemic protection exists for the individual.

### Proposed Solution

A 2D space MMO with Asteroids-inspired flight feel, deep but accessible systems, and deliberate checks and balances that protect the solo player experience. NPC infrastructure provides a permanent service floor that prevents market capture. Organisational power is real but rule-bound. The procedurally infinite frontier ensures there is always new territory no one has claimed.

---

## Success Metrics

### What Success Looks Like

Belter Life is a passion project. Success is defined by player engagement and ecosystem health, not revenue.

**Primary success indicator:** Players return. Specifically, players introduced via word of mouth play repeatedly over weeks and months — not just once out of curiosity.

### User Success Metrics

| Metric | What it measures |
|---|---|
| **Profession diversity** | Are multiple playstyles active — miners, surveyors, traders, crafters, vigilantes? Tracked via market activity and ship loadout diversity |
| **Solo player viability** | Are unaligned players progressing and returning without joining an org? |
| **Session completion** | Can a player do something meaningful and satisfying in under an hour? |
| **Retention** | Do players return after their first session? After a week? |
| **Ecosystem interdependency** | Is survey data being bought? Are mining contracts being fulfilled? Are player-crafted ships in use? |

### Business Objectives

| Timeframe | Objective |
|---|---|
| **Launch** | Family and friends are playing and sharing with their networks |
| **3 months** | A small but active community has formed; multiple professions are in regular use |
| **Ongoing** | Players return regularly; the ecosystem feels alive without developer intervention |

### Key Performance Indicators

- Market activity across multiple trade categories (not just ore)
- Ship loadout variety across the player base
- Ratio of solo to org-aligned players remaining healthy
- New player retention beyond first session

### Platform Constraint *(critical)*

Belter Life targets **any device with a reasonably large screen** — PC, tablet (iPad and Android), delivered via web browser. Mouse and keyboard are supported on desktop; touch controls on tablet. The interaction model is designed to the *touch ceiling* — no mechanic requires more inputs than are achievable on a touchscreen. This ensures full feature parity across platforms and keeps controls accessible to all players.

UI follows a **contextual complexity** model — controls remain minimal during flight, with richer interaction panels surfacing only when in proximity to relevant objects (asteroids, stations, beacons). Inspired by Minecraft's approach to touch-friendly contextual interaction.

---

## MVP Scope

### Core Features (v1)

**Flight & World**
- Assisted Newtonian flight model — touch (virtual joystick) + mouse/keyboard
- Living asteroid belt: accretion, collision fragmentation, composition variety, vaporisation events
- Procedurally generated, ever-expanding frontier
- Multi-server architecture to support a truly large world

**Mining & Economy**
- Asteroid scanning and mining mechanics
- Asteroid claiming with upkeep costs
- Global marketplace — browse anywhere, collect physically, information delivered instantly
- NPC stations with dynamic pricing (economic floor/immune system)
- Mining contracts — post and fulfil (core new player on-ramp)

**Information Economy**
- Survey data: composition and trajectory as tradeable products
- Survey ship specialisation (sensor trade-offs vs. other capabilities)
- Basic beacon placement on claimed asteroids

**Ships**
- Starter ship always available (death floor)
- Personal fleet (stored at stations)
- Basic ship loadout customisation with meaningful trade-offs (sensors vs. cargo vs. weapons etc.)
- Thruster variety (rotational, retro, main) with power/efficiency ratings

**UI/Platform**
- Web browser delivery (PC + tablet)
- Contextual UI — minimal during flight, rich panels near interactive objects

### Out of Scope for MVP

- Player-built stations and infrastructure modules
- Organisation system and faction politics
- Law, witness, and sheriff system
- Vigilante/bounty hunter profession
- Asteroid steering/thruster attachments
- Ship crafting from raw materials (NPC ships only in v1)
- NPC factions as active frontier competitors

### MVP Success Criteria

- Players can complete a satisfying session in under an hour across at least 2-3 different professions
- Information economy is active — survey data is being bought and sold
- The frontier feels genuinely explorable — new territory exists beyond settled space
- Works playably on both iPad and desktop browser

### Future Vision

- Player-built stations and modular infrastructure
- Full organisation system with territory, politics, and checks and balances
- Law and order: witness mechanic, standing, sheriffs, bounties
- NPC factions as active frontier competitors
- Ship crafting and advanced material tiers
- Asteroid steering for claim management

### Design Philosophy: Ecosystem Over Archetype

Belter Life is not designed around a single primary player type. Success is defined by a healthy, interdependent ecosystem where diverse playstyles coexist and create value for each other. No role is mandatory; no role is dominant.

### Player Archetypes

**The Miner** — Works claimed asteroids or fulfils contracts for others. The economic backbone of the belt. Solo-viable, scales up with organisation support.

**The Explorer** — Pushes deep into unmapped frontier space, maximising scan range and managing fuel limits. Sells survey data and discovery records. Chases the thrill of being first. Feeds the intelligence economy.

**The Surveyor/Cartographer** — Maps asteroid compositions and trajectories, maintains regional catalogues, plants beacon networks. Sells information. Becomes the vanguard of civilisation expansion.

**The Trader/Hauler** — Reads the marketplace, moves physical goods between stations, arbitrages regional price differences. Pure economic and logistical gameplay.

**The Crafter/Shipbuilder** — Transforms raw materials into components and ships. Supplies the entire player economy. Solo-viable as a specialist.

**The Vigilante/Bounty Hunter** — Monitors for transgressions, pursues players with active bounties, operates within the legal framework. Legitimate solo PvP with sanctioned purpose.

**The Faction Player** — Operates within a player organisation: builds stations, claims territory, extends jurisdiction, coordinates large-scale economic and political goals. The game's deepest systems reward coordination.

### Constitutional Rule: Neutral Players Are Untouchable

Organisation defences and org-vs-org warfare **never** targets unaligned neutral players. Solo players who choose not to join an organisation are permanently outside the blast radius of faction conflict. This is foundational and non-negotiable — it is the game's social contract with solo and casual players.

### User Journey

- **Day 1:** Spawn with a basic starter ship. Take a mining contract or explore a nearby sector. Learn flight feel and basic mechanics. No org required.
- **Early game:** Build a fleet, choose a profession, develop standing with NPC factions.
- **Mid game:** Build a claim portfolio — identify valuable asteroids, weigh upkeep costs against resource income, sell mining rights or post contracts. Optionally begin building infrastructure to project influence. Choose whether to align with a faction or remain neutral and free.
- **Late game / org play:** Build stations, expand into frontier, compete for regional influence, extend law jurisdiction, direct defences against rival organisations.
- **Any session:** Log in, do something meaningful in under an hour, log off without penalty. Personal situation is preserved; the world moves on but your assets remain yours.

---

### Key Differentiators

- Physics-driven living asteroid belt (accretion, collision, composition matter)
- Information as a first-class tradeable commodity
- Multiple viable solo professions: surveyor, miner, crafter, hauler, escort
- Organisations are optional enrichment, not mandatory for progress
- NPC station network as economic immune system against exploitation
- NPC factions as active frontier competitors: NPC organisations expand into new territory alongside players, competing for claims and establishing outposts — keeping the universe alive and contested regardless of player population
- Procedurally infinite frontier — always somewhere new, always first-mover opportunity
- Accessible session design: meaningful play in under an hour, no penalty for offline time
