create schema if not exists survival_manager;

create table if not exists survival_manager.profiles (
    profile_id text primary key,
    display_name text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists survival_manager.hero_instances (
    hero_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    name text not null,
    archetype_id text not null,
    race_id text not null,
    class_id text not null,
    positive_trait_id text not null,
    negative_trait_id text not null,
    equipped_item_ids jsonb not null default '[]'::jsonb
);

create table if not exists survival_manager.inventory_items (
    item_instance_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    item_base_id text not null,
    affix_ids jsonb not null default '[]'::jsonb,
    equipped_hero_id text not null default ''
);

create table if not exists survival_manager.currencies (
    profile_id text primary key references survival_manager.profiles(profile_id) on delete cascade,
    gold integer not null default 0,
    trait_reroll_currency integer not null default 0
);

create table if not exists survival_manager.unlocked_permanent_augments (
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    augment_id text not null,
    primary key (profile_id, augment_id)
);

create table if not exists survival_manager.run_summaries (
    run_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    expedition_id text not null,
    result text not null,
    gold_earned integer not null default 0,
    nodes_cleared integer not null default 0,
    completed_at_utc timestamptz not null default now()
);
