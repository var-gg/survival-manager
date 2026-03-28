# Item and Affix System

- Status: draft
- Last Updated: 2026-03-29
- Phase: prototype

## Purpose

This document defines the item and affix direction for MVP.
The target inspiration is Torchlight / ARPG-style loot identity, but the MVP implementation must remain narrow.

## MVP Rules

### Equipment Slots

Each hero has 3 equipment slots:

- weapon
- armor
- accessory

### MVP Item Model

The MVP should implement:

- base items
- simple affixes

The MVP should not begin with a deep layered loot taxonomy.
The aim is to capture loot differentiation with low data complexity.

### Design Intent

Items should help make heroes of the same archetype feel different.
A hero can diverge through:

- trait roll
- equipped items
- augment choices

This should be enough to create role variation without huge class trees.

### Crafting Scope

Crafting is not a full MVP feature.
The only recommended MVP crafting-adjacent support is:

- gold-based reforging or reroll

Special-material crafting and complex recipe systems should remain deferred.

## Long-Term Expansion Points

- broader base-item families
- richer affix pools
- rarity ladders
- set-like synergies
- advanced crafting systems
- material-driven recipe systems

## Economic Runaway Risks

- if item reroll is too cheap, item drops lose meaning
- if affix values swing too wide, one lucky item can dominate the loop
- if item acquisition is too generous, recruitment and augment choices lose relative weight

## Balance Risks

- accessory slot effects may become mandatory if utility affixes are too strong
- simple affixes may still produce dominant combinations if slot identity is weak
- reforging may become the optimal sink and crowd out other economy decisions

## Data Expansion Points

- base item definitions
- affix definitions
- slot restrictions
- reforging cost tables
- drop pool definitions

## Open Questions

- how many base item families are enough for MVP readability?
- should affixes be mostly numeric or include a few simple behavioral hooks?
- how often should the player replace gear during a short expedition arc?
- what is the safest first version of gold-based reroll so it feels useful but not mandatory?
