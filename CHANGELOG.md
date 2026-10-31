# Changelog

## 2026-10-25
Title: Customize, View Models, Armature and Server
### Added
# Models
Favorites - right-click a model or click its heart to keep it at the top of the grid; a favorite the server no longer offers is hidden, not lost
Categories - models are grouped into Official, Mounts, Facepunch and Other; each group folds away, starts folded and remembers how you left it
Facepunch models - a second search finds Facepunch's own characters on sbox.game
Mounts - characters from mounted games are offered once they pass the same check, grouped by game (Counter-Strike, Half-Life, ...)
Mounted textures - a mounted model whose own shader doesn't load (compiled for an older s&box, or not found) shows its textures on the standard shader instead of the error material: on its body, in the preview, on its tile (drawn once and kept on your machine) and as first-person arms, on Windows and Linux alike
Offline notice - when sbox.game or the Workshop can't be reached, the grid says so the way the Effects page does
Search - type in the box above the grid to find a model by name; every match shows, folded groups or not
# Customize
Skins - pick one of the model's skins, where it has more than one
Saved looks - save what you made under a name and put it on again later, per model
Part colors - tint single parts of a model (jacket, hair, eyes) as well as the whole model
Body Shape - height, head, body, arm, hand, leg and foot sliders that work on any model, morphs or not
Body groups - choose heads, capes and accessories by name; hovering a choice points it out on the preview, and no choice can hide part of the model
Apply and Revert - changes show in the preview first and reach everyone when you press Apply
# Armature
Armature - give a bone to each body part the automatic setup couldn't find, with the skeleton drawn on the preview
Set up armature - a model the automatic check refuses can be set up by hand and worn
Locked presets - parts the automatic setup found can't be changed; only missing parts take a bone, and picks are saved for the model
# View Model
View Model - choose your first-person arms from arms that Sandbox's weapons can move, grouped like the models
Your model's arms - your weapons show the arms of the model you wear, moved by the weapon's own animation; a switch on the View Model page turns it off
# Server
Server page - the host decides which models players can wear: models from sbox.game, from mounted games, or neither; the default citizen and the game's own models always stay
Blocking - the host can block any other model right from the grid; whoever wears it goes back to the citizen
Choose a player's model - the host can put any model on any player, and Revert gives them their own back
Armature switch - the host can stop players from picking bones by hand; the bones they picked are dropped
Saved rules - the host's choices are kept on their machine and used whenever they host
Long lists - the Server page shows players and blocked models ten at a time, latest blocked first, with a search box once there is more than one page
# Preview
Zoom - scroll over the preview to zoom in towards the face
### Improved
# Player Model Selector window
Tab - the selector is a Custom Player Model tab in Sandbox's own window, after HUD and Physgun, shown while a selector is in the world, instead of a window next to Saves and Effects; it fits the screen it is on
Scrolling - the mouse wheel scrolls the panes at an easy pace, and every scrollbar can be dragged
Style - the tab follows Sandbox's own theme, dark or light, and keeps a copy of it for anything a Sandbox update renames or removes
Hearts - favoriting pops the heart and eases the model into the Favorites row
### Fixed
# Models
Floating feet - every model's planted foot now stands on the ground, whatever its leg length
Frozen legs - models whose body bones are outnumbered by hair and cloth bones walk again (Humanoid Retargeter fix)
Crumpled chests - models whose spine bends back on itself no longer twist the chest (Humanoid Retargeter fix)
Weapon effects - beams and muzzle flashes start from the model's own hand
Stacked bodies - a player no longer ends up wearing several copies of their model after many changes
Skateboard riders - riding a skateboard no longer turns you back into the citizen: your model rides, for everyone, dressed the same; any addon showing a player as a body of its own can have it too (README, Other addons)
# Preview
Stacked models - the preview no longer piles up copies of the model after a reload
### Known Issues
# Models
Old shaders - a package whose shader was built for an older s&box shows the error material in its bind pose
Source mounts - Half-Life 2 and Counter-Strike: Source need s&box's Source 1 mount, which not every install has
Mounted colors - mounted models take the whole-model tint only, not part colors
# View Model
Mounted arms - Counter-Strike and Half-Life have no first-person arms of their own, so View Model's Mounts group is empty

## 2026-10-24
Title: First Release
### Added
# Player Model Selector window
Player Model Selector window - hold C and it sits next to Saves and Effects; hover a tile to preview a model, click it to wear it
Preview - the model the way it is worn, spun by dragging; a model that can't be worn says why
Default - one click back to the citizen in your own outfit
# Models
Any rigged humanoid - posed live from Sandbox's animation by the embedded Humanoid Retargeter, ragdolls included
Stock characters - Human Female and Classic Citizen, dressed in your own avatar outfit
Held weapons - the physgun, toolgun and every other weapon show in the model's own hand, aimed where you aim
Facing - models bound facing sideways (Skeleton Citizen) stand facing forward
sbox.game models - the window searches sbox.game for player models and only shows the ones that are actually rigged
# Multiplayer
Picks - everyone sees everyone's model, and your pick comes back the next time a selector is spawned
### Known Issues
# Models
First open - sbox.game models fill in over a minute or so the first time, while each one is checked
