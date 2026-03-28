insert into survival_manager.profiles (profile_id, display_name)
values ('default', 'Player')
on conflict (profile_id) do nothing;

insert into survival_manager.currencies (profile_id, gold, trait_reroll_currency)
values ('default', 0, 0)
on conflict (profile_id) do nothing;
