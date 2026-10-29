# Player Model Selector

Pick your player model in Sandbox's C menu. Spawn the Player Model Selector from the spawn menu and everyone gets a
Player Model heading on the C menu's top bar that opens the Player Model Selector window; click a model to wear it and
everyone sees it. Any rigged humanoid works, from the game, sbox.game or a mounted game: the embedded Humanoid
Retargeter poses it from Sandbox's own animation every frame, so it walks, crouches, swims, holds weapons and ragdolls
like the citizen. Favorite models, customize the one you wear (skin, colors, body shape, body groups, morphs) and save
looks, give bones by hand to body parts the automatic setup couldn't find, and see your model's own arms in first
person. The host decides which models their server allows and can choose a model for any player.

## Requirements
Sandbox (`facepunch.sandbox`). Nothing else: Humanoid Retargeter's core is compiled into this addon.

## Install
In Sandbox, open the spawn menu's **Entities** tab and search for **Player Model Selector**
(`notpointless.chomnr_player_model_selector` on sbox.game), or open this project in the s&box editor, where it is under
Entities > World.

## Quick start
### In the editor
1. Open the addon (`player-model-selector.sbproj`) in the s&box editor and press Play.
2. Spawn **Player Model Selector** from the spawn menu (Entities > World).
3. Hold **C** and click **Player Model** on the bar along the top, after Client, NPCs and Server. The **Player Model
   Selector** window opens under it; click the heading again to close it.
4. **Models**: hover a tile to preview it, click it to wear it. Models are grouped into Official, Mounts (a group per
   mounted game: Counter-Strike, Half-Life, ...), Facepunch and Other. Click a group's heading to open or fold it; groups
   start folded and stay the way you left them. Type in the search box to find a model by name. Right-click a tile (or
   click its heart) to favorite it; favorites come first. **Default** puts you back to the citizen. In the preview, drag
   to spin and scroll to zoom in.
5. **Customize**: change the model you wear. **Saved Looks** keeps what you made under a name (Save) and puts it back
   (Use). Skin picks one of the model's skins when it has several; Color tints the whole model or one part at a time; Body Shape sliders
   (height, head, body, arms, hands, legs, feet) work on any model; Body Groups and Morphs show when the model has them.
   A body group only offers choices that show something, so no part of a model can be hidden. Hover a part to see it on
   the preview. Nothing changes for others until you press **Apply** (top right);
   **Revert** drops the changes.
6. **Armature**: body parts the automatic setup found are locked. A part it couldn't find can be given a bone: pick
   the part on the left, then click its bone on the right; the preview draws the skeleton and lights up the bone. Press
   **Apply**; the picks are saved for that model. A model the automatic check refuses offers **Set up armature** in the
   grid.
7. **View Model**: **Use my player model's arms** (on by default) shows the arms of the model you wear on your weapons.
   Below it are first-person arms, grouped the same way: click one to use it when your model's arms aren't used (switch
   off, or a stock character, whose arms are Sandbox's own).
8. **Server** (the host only): choose which models players can wear (sbox.game, mounted games), whether they can set up
   armatures, and see the blocked models. Hover a tile in the grid and click its block icon to block it. Under Players,
   **Choose model** opens the grid to put a model on that player; **Revert** gives them their own pick back. Both lists
   show ten at a time (latest blocked first) and get a search box once they run past one page.

### In code
None: the selector is a component, and picks go through the Player Model Selector window.

## Options
Set on the `PlayerModelSelector` component of `Assets/entity/player_model_selector.prefab`.

| Option | Default | What it does |
|---|---|---|
| Models | Human Female, Classic Citizen | Models always offered, after Default. Each entry is a title and a model: a path that ships with the game (`models/.../x.vmdl`) or an sbox.game ident (`org.model`). The defaults are set in code; a path saved into the prefab is packed into the published entity with everything it uses. |
| Cloud Search | `type:model playermodel sort:popular` | The sbox.game search the window fills itself from. Only the rigged humanoids it finds get a tile. |
| Cloud Count | 50 | How many results each search looks at. |
| Facepunch Search | `type:model org:facepunch human` | A second search for Facepunch's own characters, for the Facepunch group. Empty turns it off. |
| View Model Searches | `type:model first person arms`, `type:model hands` | Searches the View Model grid fills itself from. Only arms Sandbox's weapons can move are shown. |

## How it works
The citizen Sandbox makes for every player keeps running, driven by Sandbox's animation, IK and ragdoll physics, but
is hidden with its clothes. A second renderer shows the picked model, and every frame `RuntimePoseRetargeter` from
Humanoid Retargeter maps the citizen's pose onto it, keeping its planted foot on the citizen's ground. What a player
wears is a look: the model with its colors, body shape, body groups, morphs and any bones picked by hand. The host
keeps everyone's look; each client loads the models, poses them and applies the looks itself. sbox.game and mounted
models are checked before they get a tile, and each verdict is remembered on your machine. In first person, the worn
model's arms stand in for the `Hands` of Sandbox's weapon view models: the weapon's skeleton has the citizen's arm bones,
so the same retargeter poses the model's arms from it, puts its joints on the weapon's and folds the rest of the model
away between the shoulders. Picked arms just replace the `Hands` model. See [DEVLOG.md](DEVLOG.md) for the decisions
behind all of this.

## Multiplayer
Works in single-player, as host and as client, checked with a second client joining the editor. Only the looks sync
(connection to look, written by the host, which accepts listed models, sbox.game idents and mounted models, and caps
the rest); every client loads, checks, poses and customizes the models locally. A player whose machine lacks a mounted
game sees the citizen for that player. First-person arms only ever show to their own player, so they don't sync.
Favorites, saved looks and the look saved for each model stay on your machine. The host's rules (allowed sources,
blocked models, the armature switch) sync to everyone and are saved on the host's machine; a model the host chooses for
a player shows in place of that player's own pick until the host reverts it. Turning the armature switch off drops the
bones other players picked by hand. A client that loads the addon's code after the selector reached it asks the host to
send it again.

## Other addons
An addon that shows a player as a body of its own while it has them (a skateboard's rider, a seated driver) gets the
player's model on that body too: tag the object holding the body's `SkinnedModelRenderer` `player_body`, and make it
under a network object the player owns (as a rider spawned with `NetworkSpawn( player )` is). Every client then shows
that player's pick on it from its first frame, posed from the body's own animation the way a citizen is, with a stock
character dressed in the player's own outfit, and takes it off with the body. Only the object's own tag counts, not one
it has through a parent, and in a lobby a tagged body that isn't under a networked object is left alone. The Skateboard
addon tags its rider this way.

## Limitations
- The first time the window opens, each sbox.game model is downloaded once to check it, so the grid fills in over a
  minute or so; after that it's instant.
- A package whose shader was built for an older s&box ("Old shader needs to be recompiled") shows the error material
  in its bind pose; that is the package, not the selector.
- Avatar clothing only goes on the stock characters, which only offer their heads as body groups and the whole-model
  tint: their clothing decides the rest.
- Part colors only show on materials whose shader has a color tint (most do). Mounted models only take the whole-model
  tint: a mounted game's materials can't be copied.
- Body groups can't hide a part: a group's empty choice is only offered when it is the model's own default (a cape
  that starts off), and a look asking for any other empty choice shows the default instead.
- Body group outlines can look faint: outlines only partly render inside the preview's own scene. Colored parts pulse
  instead of being outlined.
- First-person arms must use Sandbox's arm and finger bone names (`arm_upper_R`, `hand_R`…); arms with other bones
  need converting with Humanoid Hand Retargeter first.
- Mounted games only show once s&box has been restarted after installing them. Half-Life 2 and Counter-Strike: Source
  need s&box's Source 1 mount, which isn't in every s&box install (`sbox/mount/source1`). Some mounted models have no
  recognisable bones (Counter-Strike's HD GIGN) and offer Set up armature instead. Counter-Strike and Half-Life have no
  first-person arms of their own (their view models have the weapon built in), so View Model's Mounts group says so.
- Bones the automatic setup found can't be changed by hand (they are locked, for every look on every machine); a model
  whose automatic mapping is wrong rather than incomplete needs a Humanoid Retargeter fix.
- When sbox.game or the Workshop can't be reached, the grid says so and lists only the game's own and mounted models.
- Held weapons keep the citizen's grip angle, so they aim where the player aims; on very different arms the grip can
  look a little off.
- Your model's arms in first person keep the model's own finger lengths, so fingers can sit a little off the grip. A
  weapon whose view model lacks the citizen's arm bones shows the picked arms instead.
- The default citizen and the game's own models (the component's Models list) can't be blocked, so there is always
  something to wear.
- Search finds the models the grid has loaded; sbox.game models still being checked show once they pass.
- One selector per world; a second one removes itself.

## Development
- Code: `Code/PlayerModels` (the addon), `Code/HumanoidRetargeter/Core` (Humanoid Retargeter's core, copied unchanged from
  `02-plugins/humanoid-retargeter`, see DEVLOG for the commit).
- Styles import `Code/PlayerModels/UI/SelectorTheme.scss`, a copy of the parts of Sandbox's theme they use, so a
  Sandbox restyle can't break them.
- Compile check outside the editor: `dotnet build Code/chomnr_player_model_selector.csproj` (gitignored, needs a
  `package.facepunch.sandbox.dll` from an addon the editor has opened). The editor build is the one that applies the
  whitelist and compiles the razor files the same way the game does.
- Publishing: publish the entity, not the project. In the editor's Asset Browser, right-click
  `Assets/entity/player_model_selector.sent` and choose Publish. It has Include Code on, so the addon's code and its
  stylesheets go with it, and the prefab brings everything else; the result is a `sent` package, the kind Sandbox's
  spawn menu lists under Entities. Keep `Libraries/` empty, and keep model paths out of the prefab: anything it names is
  packed with the entity (the citizen statue is made from code by `SelectorMannequin` for that reason).
- Testing with a second client from the editor: the client gets the editor's code but not the addon's files, so the
  window shows unstyled there (and the client can't find the prefab). The published package carries both.

## License
Humanoid Retargeter's core keeps its notices: see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
