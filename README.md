# Player Model Selector

Pick your player model in Sandbox's C menu. Spawn the Player Model Selector from the spawn menu and everyone gets a
Custom Player Model tab in Sandbox's window, next to HUD and Physgun; click a model to wear it and everyone sees it. Any
rigged humanoid works, from the game, sbox.game or a mounted game: the embedded Humanoid Retargeter poses it from
Sandbox's own animation every frame, so it walks, crouches, swims, holds weapons and ragdolls like the citizen. Favorite
models, customize the one you wear (skin, colors, body shape, body groups, morphs) and save looks, give bones by hand to
body parts the automatic setup couldn't find, and see your model's own arms in first person. The host decides which
models their server allows and can choose a model for any player.

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
3. Hold **C** and open the **Custom Player Model** tab in Sandbox's window, after HUD and Physgun.
4. **Models**: hover a tile to preview it, click it to wear it. Models are grouped into Official, Mounts (a group per
   mounted game: Counter-Strike, Half-Life, ...), Facepunch and Other. Click a group's heading to open or fold it; groups
   start folded and stay the way you left them. Type in the search box to find a model by name. Right-click a tile (or
   click its heart) to favorite it; favorites come first. **Default** puts you back to the citizen. In the preview, drag
   to spin and scroll to zoom in.
5. **Customize**: change the model you wear. **Saved Looks** keeps what you made under a name (Save) and puts it back
   (Use). Skin picks one of the model's skins from a dropdown when it has several; Color tints the whole model or one
   part at a time; Body Shape sliders (height, head, body, arms, hands, legs, feet) work on any model; Body Groups and
   Morphs show when the model has them. A body group can hide an accessory (a hat, a weapon, a pet), never the body
   itself, so nobody turns invisible. Hover a part to see it on the preview. Nothing changes for others until you press
   **Apply** (top right); **Revert** drops the changes.
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
| Cloud Search | `type:model playermodel sort:popular` | The sbox.game search the window fills itself from. Only the rigged humanoids it finds get a tile; packages tagged hand, hands, arm, arms, clothing or citizen, and models sbox.game says have fewer than 15 bones or are under 48 units across (hats, shirts), are left out of every body search. |
| Cloud Count | 50 | How many results each search looks at. |
| Facepunch Search | `type:model org:facepunch human` | A second search for Facepunch's own characters, for the Facepunch group. Empty turns it off. |
| Human Search | `type:model category:human sort:popular` | sbox.game's Human category, under Other (Facepunch's own under Facepunch). Empty turns it off. |
| More Searches | none | More searches for player models. Every search's next page is fetched when you reach the end of Other or Facepunch. |
| View Model Searches | `type:model first person arms`, `type:model hands` | Searches the View Model grid fills itself from. Only arms Sandbox's weapons can move are shown. |

## Other addons
An addon that shows a player as a body of its own while it has them (a skateboard's rider, a seated driver) gets the
player's model on that body too: tag the object holding the body's `SkinnedModelRenderer` `player_body`, and make it
under a network object the player owns (as a rider spawned with `NetworkSpawn( player )` is). Every client then shows
that player's pick on it from its first frame, posed from the body's own animation the way a citizen is, with a stock
character dressed in the player's own outfit, and takes it off with the body. Only the object's own tag counts, not one
it has through a parent, and in a lobby a tagged body that isn't under a networked object is left alone. The Skateboard
addon tags its rider this way.

## License
Humanoid Retargeter's core keeps its notices: see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
