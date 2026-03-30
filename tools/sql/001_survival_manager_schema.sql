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

create table if not exists survival_manager.hero_loadouts (
    hero_id text primary key references survival_manager.hero_instances(hero_id) on delete cascade,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    equipped_item_instance_ids jsonb not null default '[]'::jsonb,
    equipped_skill_instance_ids jsonb not null default '[]'::jsonb,
    passive_board_id text not null default '',
    selected_passive_node_ids jsonb not null default '[]'::jsonb,
    equipped_permanent_augment_ids jsonb not null default '[]'::jsonb
);

create table if not exists survival_manager.hero_progressions (
    hero_id text primary key references survival_manager.hero_instances(hero_id) on delete cascade,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    level integer not null default 1,
    experience integer not null default 0,
    unlocked_passive_node_ids jsonb not null default '[]'::jsonb,
    unlocked_skill_ids jsonb not null default '[]'::jsonb
);

create table if not exists survival_manager.skill_instances (
    skill_instance_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    skill_id text not null,
    slot_kind text not null,
    compile_tags jsonb not null default '[]'::jsonb
);

create table if not exists survival_manager.passive_selections (
    hero_id text primary key references survival_manager.hero_instances(hero_id) on delete cascade,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    board_id text not null default '',
    selected_node_ids jsonb not null default '[]'::jsonb
);

create table if not exists survival_manager.permanent_augment_loadouts (
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    blueprint_id text not null,
    equipped_augment_ids jsonb not null default '[]'::jsonb,
    primary key (profile_id, blueprint_id)
);

create table if not exists survival_manager.squad_blueprints (
    blueprint_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    display_name text not null,
    team_posture text not null,
    team_tactic_id text not null default '',
    deployment_assignments jsonb not null default '{}'::jsonb,
    expedition_squad_hero_ids jsonb not null default '[]'::jsonb,
    hero_role_ids jsonb not null default '{}'::jsonb
);

create table if not exists survival_manager.active_runs (
    run_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    expedition_id text not null,
    blueprint_id text not null,
    is_quick_battle boolean not null default false,
    current_node_index integer not null default 0,
    temporary_augment_ids jsonb not null default '[]'::jsonb,
    pending_reward_ids jsonb not null default '[]'::jsonb,
    battle_deploy_hero_ids jsonb not null default '[]'::jsonb,
    compile_version text not null,
    compile_hash text not null default '',
    last_battle_match_id text not null default ''
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

create table if not exists survival_manager.match_record_headers (
    match_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    run_id text not null,
    content_version text not null,
    sim_version text not null,
    seed integer not null,
    player_snapshot_hash text not null,
    enemy_snapshot_hash text not null,
    started_at_utc timestamptz not null,
    completed_at_utc timestamptz not null,
    winner text not null,
    final_state_hash text not null
);

create table if not exists survival_manager.match_record_blobs (
    match_id text primary key references survival_manager.match_record_headers(match_id) on delete cascade,
    compile_version text not null,
    compile_hash text not null,
    input_digest text not null,
    event_stream jsonb not null default '[]'::jsonb,
    keyframe_digests jsonb not null default '[]'::jsonb
);

create table if not exists survival_manager.inventory_ledger (
    entry_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    run_id text not null,
    item_instance_id text not null,
    item_base_id text not null,
    change_kind text not null,
    amount integer not null,
    created_at_utc timestamptz not null,
    summary text not null
);

create table if not exists survival_manager.reward_ledger (
    entry_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    run_id text not null,
    reward_id text not null,
    reward_type text not null,
    amount integer not null,
    created_at_utc timestamptz not null,
    summary text not null
);

create table if not exists survival_manager.suspicion_flags (
    flag_id text primary key,
    profile_id text not null references survival_manager.profiles(profile_id) on delete cascade,
    run_id text not null,
    match_id text not null,
    reason text not null,
    expected_hash text not null,
    observed_hash text not null,
    created_at_utc timestamptz not null
);
