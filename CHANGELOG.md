# Changelog

## 2026-10-25
Title: Customize, View Models, Armature and Server
### Added
# Models
Favorites - right-click a model or click its heart to keep it in the Favorites group, the first tile of the grid on both the Models and View Model pages, there even before the first favorite; a favorite the server no longer offers is hidden, not lost
Groups - models are grouped into Official, Facepunch, Other and one group per mounted game, each shown as a tile (a game with its own logo); click one to see its models twenty at a time, with Previous and Next
Facepunch models - a second search finds Facepunch's own characters on sbox.game
Human models - sbox.game's Human category (over 300 models) is listed too, under Other, less the hands, arms, clothing and citizens and the models with too few bones for a body
More models - every sbox.game search lists its next page when you reach the end of Other or Facepunch, so a long category isn't fetched all at once
Not bodies - sbox.game packages tagged hand, hands, arm, arms, clothing or citizen aren't offered as bodies, and a model sbox.game says has fewer than 15 bones, or is under 48 units across (a hat, a shirt, hair rigged to the citizen), leaves the grid at once, without being downloaded; mounted models under that size are refused too
No materials - an sbox.game package published without its materials (only the error checkerboard to show) is refused when picked and leaves the grid
Checked as you look - a model is checked only while its group is open (or it matches the search), several at a time, so a game or search with thousands of models never loads them all at once; with the menu closed, the first page of each group is checked ahead from the moment the selector is in the world, so the menu opens on models already checked
Download on pick - an sbox.game model downloads only when you pick it, never while you browse or hover: its tile shows a download icon until then, and while it downloads the tile lightens from left to right (an estimate from the package's size and earlier downloads, as s&box doesn't report a download's progress); picking another model meanwhile drops the download, so sbox.game not answering never leaves the grid stuck on it
Caching - searches are kept for an hour and every check for as long as its package isn't updated, so starting or joining again doesn't ask sbox.game again
Mounts - characters from mounted games are offered once they pass the same check, a group per game; they are looked for in folders named like player, humans, characters, heroes, npc, actors and peds, and a game whose folders say nothing offers all its models, characters by name first, so any mount works without setting anything up; a game mounted while you play joins them within 5 seconds
Model folders - the folder button next to the search box lists each mounted game's model folders with how many models each holds, each game folding away like the grid's groups; turn a folder on and its models come first in its game's group, each checked as it is shown
Mounted textures - a mounted model whose own shader doesn't load (compiled for an older s&box, or not found) shows its textures on the standard shader instead of the error material: on its body, in the preview, on its tile (drawn once and kept on your machine) and as first-person arms, on Windows and Linux alike
Offline notice - when sbox.game or the Workshop can't be reached, the grid says so the way the Effects page does
Search - type in the box above the grid to find a model by name; every match shows, folded groups or not
# Customize
Skins - pick one of the model's skins from a dropdown, where it has more than one
Saved looks - save what you made under a name and put it on again later, per model
Part colors - tint single parts of a model (jacket, hair, eyes) as well as the whole model
Body Shape - height, head, body, arm, hand, leg and foot sliders that work on any model, morphs or not
Body groups - choose heads, capes and accessories by name, or hide an accessory (a hat, a weapon, a pet); hovering a choice points it out on the preview; the body itself (head, chest, legs, arms, hands, feet) can never be hidden, so nobody turns invisible
Body group colors - each body group's color is right under its choices, for the choice picked
Apply and Revert - changes show in the preview first and reach everyone when you press Apply
Advanced editor - the Advanced editor button on the Customize page opens a window over the whole screen with your model in the middle, the options down the right, and Share mine, Copy code, Revert, Apply and Close along the bottom; your character stands still while it is open
Camera - the Advanced editor's view moves the way Blender's does: middle-drag orbits, Shift+middle pans, Ctrl+middle or the wheel zooms (Alt+left orbits without a middle button), numpad 1, 3 and 7 look from the front, right and top (Ctrl for the other side), 2, 4, 6 and 8 step around, Home frames the body and numpad . the selected item; Walk (Shift+`) flies with WASD, Q and E; the model turns in the room left of the options
Gizmo - click an item in the Advanced editor to select it and move, turn and size it with Blender's gizmo (arrows, rings and boxes, from the Move, Rotate, Scale or Transform tool) or Blender's keys: G, R and S, then X, Y or Z for an axis (Shift for a plane), a typed number, Ctrl to snap and Shift for precision, left click or Enter to keep and right click or Esc to cancel; Alt+G, Alt+R and Alt+S clear, Shift+D duplicates, Delete removes, and Ctrl+Z and Ctrl+Shift+Z undo and redo; every move stays within the item's caps
Animation graph - the Advanced editor says which animation graph system moves your model, "AnimGraph1 (legacy)" or "Using AnimGraph2" on s&box builds that have AnimGraph2
Styles - save what you made as a style in the Advanced editor, Switch to one to edit it, and Save changes to keep them; styles are yours alone, and editing one never changes what you published from it
Material finishes - give the whole model or single parts one of 8 stock finishes (gold, silver, chrome, copper, bronze, gloss, clay, matte black), in the Advanced editor; they take colors, and they work on mounted models, which colors of single parts don't
Hide parts - hide a single part of a model from the Finish list in the Advanced editor, such as a dress that isn't a body group; skin and body parts can't be hidden, and neither can the last part showing, on anyone's screen
Accessories - wear up to 8 cosmetics on any model, in the Advanced editor: every stock item and everything on the Workshop (1,500+), picked from a list of categories down the side, with pictures and a search; glasses, hats, hair and the like are pinned to a joint and can be moved, turned and sized, clothes bend with the body, and each has its own colors, finish, body groups and morphs; an item stays under half the model's height and within a quarter of it from its joint, on everyone's screen, and other players download the Workshop items you wear, which count towards your download limit on the server
Shared looks - Share mine, under the Customize options, publishes your look for a model (skin, colors, finishes, body groups, morphs, body shape, items) where every player of the selector can find it; the Workshop button beside Reset everything opens the looks players shared, rated and a page at a time like shared armatures, where Use puts one on the preview and Apply wears it; Copy code puts it on the clipboard as one line of text
Share details - sharing a look or an armature asks what to call it (up to 32 characters) and for a description (optional, up to 128); the Workshop lists each by its name with its author's Steam avatar and name as they are now, and the info button beside one shows its description, its author and everything else they shared
Publishing - what you publish stays as it was published: to share changes, publish again as a new one (5 at once per model); your own can be taken down from the info button
Workshop status - a red bar reads "Facepunch Workshop is currently unresponsive" while Facepunch's service isn't answering, and goes away by itself when it is back (asked every 30 seconds)
# Effects
Effects - the Effects tab beside Customize puts effects on your model from 16 presets (glowing eyes, fire hands, sparkle halo, holy aura, dark aura, hearts, snow cloud, rain cloud, electric body, magic orbs, flame head, lantern hand, speed trail, sword slash, confetti burst and rainbow), each tile moving as its effect does; the preview shows them at once and Apply wears them, up to 8 at a time
Effect sliders - Color, Size, Amount and Speed change the selected effect all at once: Color turns its colors round the color wheel, Size and Speed go from half to twice, Amount from a quarter to four times; switch an effect off and on, or remove it, in the list of effects you wear
Effect keys - give an effect a key (letters, digits, F1 to F12 or the side mouse buttons, suggested as you type, with the key's picture beside it) and choose what it does: show the effect for 10 seconds, switch it on and off, or show it while held, for everyone, fading in and out over half a second; a player who joins meanwhile sees it for the time it has left; starting an effect with a key works once every 2 minutes for each player, whichever effect it is for, and the host holds everyone to that (and every machine refuses a start inside it), while switching one off or letting go is always free
Clan tag colors - Rainbow colors its sprites one by one with World at War's scrolling clan tag colors (rain); the selected effect's colors can be changed, added to (any number), removed (Remove, down to one) or put back to the game's five under its sliders
What you see - Effects quality (High, Normal, Low or Off) shows less of everyone's effects on your screen, never more than the host allows, and any other player's can be hidden; both are kept on your machine
Any model - effects are made on the citizen and worn in proportion on any model, from its eyes (each eye or between them), the top of its head (however high it reaches over its head bone), head, neck, chest, hips, hands or feet; a joint the model doesn't mark is stood in for, so an effect always shows
Drawn on your machine - effects' pictures (glow, spark, flame, smoke, sparkle, heart, snowflake, droplet) are drawn from lines on every machine, so effects add nothing to download or publish
Citizen effects - the default citizen wears effects too, without picking a model: on the Effects tab, in the preview and for everyone, kept for the citizen like a model's look
Effect layers - Edit in Advanced editor, on the Effects tab, opens the Advanced editor's Effects tab on the selected effect: add sprites, a light or a trail (up to 8 layers an effect, 32 a look), copy, reorder, switch off and remove its layers, with each layer's main settings showing and the rest under More
Comes from - an effect comes from a joint, an item worn (it follows the item as it moves, whatever its place in the list) or the tip of the weapon held; Keep it where it is keeps it in place when it moves to another, Forward, Left and Up nudge it (up to 24 units each way), and while the item is off or nothing is held it shows on the joint it was on
Shapes - sprites come from a point, a ball, a ring, all around the body, a box, a cone, a line (across, or from one joint to another) or one of 40 standard shapes (circles, polygons, stars, crosses, hearts, lines, waves, spirals and solids like a sphere, a cube or a torus), along its outline or all over it, and each sprite can look like any of the shapes; the shapes are worked out on your machine, so nothing is downloaded
Rhythm and When - a layer can pulse, flicker, go through every color, turn round its place (circling it when set off to the side) or jiggle, and show always, while you move, stand, are in the air, crouch or hold a weapon, or for a moment when you jump, land, get hurt, start or stop moving, crouch, stand up, shoot or swing, or hit something hard, with a burst of up to 100 sprites and showing for up to 5 seconds; every client works these out from what it already sees of you, and each counts at most 10 times a second (shots 20)
Trails - a layer can be a trail behind its place, from its color and width to its end color and width, lasting up to 5 seconds, and showing only as much as its place moves (a sword's swing, a runner's feet)
Mixed colors - a layer's sprites can each take one of its colors in turn and keep it, like confetti
Guides - Guides, on the Effects tab, explains what an effect, a layer, an anchor, Comes from and Looks like, Rhythm, When, keys, shapes, stickers, the Workshop (Use and Fork), the shape creator, color cycles and the budget are, and how to build an effect from layers, put one on a hat or a weapon, show a layer on a jump or a key, set up a sword slash, remix from the Workshop and draw your own shape, each with a short clip recorded in the game (19 clips, 3.0 MB)
Sums in number boxes - type a sum like 2*3+1 or (10-4)/4 in the number boxes of the Advanced editor's Effects tab: it is worked out when you press Enter or click away and held to the box's range, and anything that isn't a sum puts the number back
Drawn shapes on bodies - a drawn shape is drawn on your machine once (128 px), a millisecond or so a frame as players join, and shared by everyone wearing it, so a hundred players with the same sticker make one picture; at most 128 are kept (8 MB), and past that the furthest players show a plain circle until a nearer one is let go; a server with no screen draws none
Undo and redo - the arrows on the Effects tab and in the Advanced editor's Effects tab step back and forward through your changes to effects, 50 steps (a slider dragged is one step)
Several layers at once - Ctrl+click or Shift+click layers in the Advanced editor to choose more than one: a change goes to all of them, and only what they all have shows
My effects - save the selected effect under a name (Save to My effects, under its sliders) and put it on again from its tile on the Effects tab; up to 50, kept on your machine, each deleted from its tile's corner
Surprise me - the tile after the presets makes an effect of one to three parts of the presets and My effects, each on the joint it came from, its colors turned at random, within what this server's budget has left (its sprites thinned to fit)
Variations - Variations, under the selected effect's sliders, offers four copies of it with its colors turned, its size, amount and speed nudged and now and then a layer's rhythm or picture swapped; click one to use it, or More like it for four more
Shape creator - Draw a new shape, at the end of a layer's shape and Looks like lists in the Advanced editor, opens a canvas: draw with the mouse (each stroke smoothed and simplified, undone one at a time), as lines or filled, write text (the Hershey font, so it is the same on every machine), add a standard shape or one of yours, then mirror, flip, turn, size and fit it, up to 500 points a shape; Use as Comes from sends sprites out along it, Use as Looks like makes each sprite it
Stickers - a shape can be a sticker: its own fill and outline colors, an outline width, and a moving color (Rainbow, the clan tag Rain, Cycle and Scanner with their colors, or Pulse) at a speed; each sprite of it covers what is behind it, and the creator shows it moving as a body does
My shapes - shapes and stickers are kept by name (up to 50, on your machine), opened in the creator again to change and picked in a layer's lists; a look carries each it uses once (up to 8 a look), so everyone sees them with nothing to download, and a tampered look is clamped (500 points a shape, the rest cut)
Effects Workshop - Workshop, on the Effects tab, lists the effects and single layers players shared, for any model: Top or New, a search box, filters by where it comes from, whole effect or layer, what it has and the #tags in its description, a moving picture of each drawn on your machine, ratings, Hide author, and details with the author's other shares; Use puts a copy on (a single layer joins the effect you selected), Fork opens a copy in the Advanced editor, and both credit the share ("Based on" its name and author), kept when you publish your own
Sharing effects - Share this effect, under the selected effect's sliders, and Share this layer in the Advanced editor (beside Save layer to My effects) publish it with a name (up to 32 characters) and a description (up to 128), carrying the drawn shapes it uses; the same effect is never published twice (an unchanged fork included), each player has up to 20 published at once, a share bigger than the Workshop's 64 KB record is refused, and one read back past it is passed over
Shapes & stickers Workshop - the Workshop's second tab, and Workshop at the end of a layer's shape lists, lists the shapes and stickers players made, with the same search, filters, ratings and details; Use keeps one in My shapes (from a layer's list, it goes on the layer too), Fork opens a copy in the shape creator, and Share this shape publishes yours
Workshop pace - what a Workshop lists is kept for 20 seconds, so pages opening one after another don't ask again, and changes (publishing, rating, taking down) go at most every 1.5 seconds, within the service's limit
Ground - an effect or a layer can come from the ground under the feet (Ground (under the feet), in Comes from and From joint): below the hips at the feet's height, turned the way the body faces however it leans, so a circle there lies under the player as they walk or run
On a kill - a layer can show (and a script's OnKill run) when someone the player shot dies a moment after: every machine works it out from the shots and deaths it sees, at most 10 times a second
Lying flat - in the Advanced editor a shape the sprites come from can lie Upright, Flat on the ground (turned the way the body faces, wherever its joint leans) or Facing the camera, and each sprite can lie Facing the camera, Flat on the ground, the way the layer faces or Out from the middle (under More), so a circle on the ground under the feet reads as a magic circle on the floor as the player walks or runs; the scripting reference shows the same as code (Magic circle on the floor)
Test bar - the Effects tab's preview, the Advanced editor's and the scripting reference's each have a test bar that plays an effect's real cause on the body: Swing, Shoot at a target (tracers and impact sparks hit it), Reload, Equip and Holster, Jump, Land, Hurt, Death and Respawn, Footsteps and the effect's keys (here with no 2-minute wait), Walk or Run back and forth at a speed, Loop the last action every 2 seconds, slow motion (1x, 0.5x, 0.25x) and Pause with a step a frame; all on that preview only, nothing sent
# Scripting
Scripts in looks - an effect written as a script carries its script (minified) in your look, and every player's machine builds the effect again from it, so others see what the script makes, with the options you chose; a script is held to 16 KB (32 KB for a look's together) before anything reads it, compiled once per look change and kept by its text (a hundred players with one script compile it once), and each player's new scripts compile at most 2 a second on each machine (8 at once); one that doesn't compile shows what it made when it was saved
Scripts that run - an effect's script runs on every player's machine while the effect shows: OnUpdate each frame, its events (OnSpawn, OnKey, jumping, landing, getting hurt, crouching, starting or stopping, hitting something hard, shooting or swinging, a kill) as each happens, at most 10 times a second each (shots 20), and each layer's particle function for every sprite, moving, colouring, fading, turning and sizing it; one player's scripts run at most 60,000 instructions a frame and everyone's 600,000, a body out of view or too far to show effects runs none, and no sprite is ever drawn bigger than 128 units, whatever a script asks
Kept sprites - Layer.Keep holds up to 100 sprites out all the time, each with its own p.Index from 0, spread evenly in that order along where the layer comes from, their life looping every Lifetime: formations a particle function moves (runes round a ring, orbs following a hand)
Beams - Beam() draws a ribbon through up to 64 points kept along where the layer comes from (a line between the hands, a ring), its colour and width going from start to end along it, each point moved, coloured and sized by the layer's particle function (lightning that jitters, a helix that turns)
Ghosts - Ghost() leaves see-through copies of the body (or of the weapon held, or the item the effect comes from) where it was and posed as it was, fading over the layer's lifetime: afterimages, up to 30 a second and 8 out at once
Sprites that lie - Layer.Align lays a layer's sprites flat on the ground (turned the way the body faces), facing the way the layer faces, or facing out from its middle like tiles on a ball, in place of facing the camera; p.Rotation turns each in its plane
Presets are scripts - every stock effect, and the colour modes (Rain, Cycle, Scanner, Mixed) and rhythms (Pulse, Flicker, Rainbow, Spin, Jiggle) layers use, is a commented script in the effect language, built from it on every machine and looking as it did; a preset you put on carries its script (each well under 4 KB, so eight fit a look)
Code editor - Edit as code, on the Effects tab and in the Advanced editor, opens any effect (the presets too) as its script in a code editor: line numbers, VS Code's colours (Dark+ by default, Light+ with the theme button, kept on your machine), completion as you type (members, your own names, keys with their pictures, shapes and stickers with theirs), signature help in a call, docs on hover (More… opens the reference), a swatch before each colour (click it to pick another; the code keeps its form), a key's picture before each key, the bracket or block matching the caret, find and replace (match case, whole word, "3 of 12"), go to line, Ctrl+/ comments, Tab and Shift+Tab indent, Enter keeps the indent, brackets and quotes close themselves, Format; errors are underlined and counted (the list under the status bar), never popped up, and not while you are still typing the word
Auto-apply and Save - with Auto-apply on (the default, kept on your machine) the preview follows the code a moment after you stop typing, the last good version showing while it has an error; off, nothing changes until Save; Save (Ctrl+S) puts it on the effect and wears it for everyone, and the status bar shows its size ("3.2 KB of 16 KB") and where the caret is
Code window - drag the code editor by its title, size it from its edges and corner, maximize it (double-click the title); where it was is kept; closing it with changes asks to Save, Discard, Keep as draft or Cancel
Code and layers in step - with the Advanced editor open, every good build updates its layers and fields, and a change there is written into the code in place (the caret stays, undo works); a field the code works out (a loop, a function, a parameter) is put back with why and its line; the caret in a layer's lines selects that layer, and selecting a layer puts the caret on its lines
Drafts - Keep as draft keeps a script exactly as it is, errors and all, on your machine (up to 50, the oldest let go past that); drafts come first in My effects with a pencil in their top-left corner and open in the code editor where you left off; Save turns one into the effect
Scripting reference - the ? button opens every name a script can use, with a search, each with a live example: change it and its preview (with its own test bar) or its result follows, Reset example puts it back, and Copy into my effect puts it in your script; first pages say what a script is, how expressions work and what parameters are
Parameters - an effect's script's options ([Parameter]) show under its four sliders on the Effects tab and with it in the Advanced editor, grouped and folding by group, each as its type says: a slider with a box for sums, a check mark, a colour, a palette to add to and take from, text, a shape or picture, a key with its picture, a list of choices or three numbers; a value chosen is kept with the effect (held to its range on every machine, gone when the script no longer has it), Reset puts the script's own back and Save as default writes it into the script
# Armature
Armature - give a bone to each body part the automatic setup couldn't find, with the skeleton drawn on the preview
Shared armatures - Share mine, under the Armature page's lists, publishes your armature for a model where every player of the selector can find it; the Workshop button beside Clear my picks (with how many are shared for the model) opens the armatures players shared, a page at a time sized to fit, where Use takes the parts one gives your copy of the model onto the preview and Apply wears them; picks already shared, by you or anyone, aren't shared again, and copies shared at the same moment are listed once
Ratings - thumbs up or down on each shared armature or look, the best rated listed first; clicking your vote again takes it back; each player counts once, and never on their own share
Top and New - the Workshops list shared armatures and looks the best rated first (Top) or the newest first (New), switched at the top of the list and kept for next time
Hide author - Hide author, in a share's details, hides everything that player shares from both Workshops, on your screen only; under the list, Show them lists their shares faded, where Unhide author brings them back
Armature codes - Copy code, beside Share mine, puts your armature on the clipboard as one line of text
Shared counts - a model's tile shows how many armatures and looks are shared for it, a figure and a number in its top left corner
Set up armature - a model the automatic check refuses opens on the Armature page and stays there while you give its missing parts bones, saying what it still needs, until Apply puts it on
Locked presets - parts the automatic setup found can't be changed; only missing parts take a bone, and picks are saved for the model
# View Model
View Model - choose your first-person arms from arms that Sandbox's weapons can move, grouped like the models
Your model's arms - your weapons show the arms of the model you wear, moved by the weapon's own animation, the forearm turning with the hand so a rifle's grip doesn't wring the wrist; a switch on the View Model page turns it off
# Multiplayer
Host leaving - when the host leaves, whoever hosts next takes over the selector, with everyone's picks and the rules as they were
Distant players - other players' models are posed every frame only near you; past 750 units 30 times a second, past 2,000 units 15, and out of view 10 (for their shadows), so a full server costs less each frame; they still move with their players every frame
Effects at a distance - other players' effects show in full within 750 units, half their sprites up to 2,000 and none past that, and pause out of view; all of them together show at most 6,000 sprites and 16 lights, the nearest players first, and a crowd joining has its effects built a few at a time
# Server
Server page - the host decides which models players can wear: models from sbox.game, from mounted games, or neither; the default citizen and the game's own models always stay
Blocking - the host can block any other model right from the grid; whoever wears it goes back to the citizen
Choose a player's model - the host can put any model on any player, and Revert gives them their own back
Armature switch - the host can stop players from picking bones by hand; the bones they picked are dropped
Model folders switch - players other than the host can only use the folder button once the host turns it on (off by default); everyone gets every mounted game's characters either way
Body groups switch - the host can keep players' body group choices off; their models show their own until it is turned back on, which puts every choice back at once
Items switch - the host can keep the items players wear off; they stay with their looks and show again when it is turned back on, and Workshop items also stay off while models from sbox.game do
Mounted games - the Server page lists the games the players here have mounted, ten at a time with a search box, and the host can block any of them; whoever wears a model from a blocked game goes back to the citizen
Saved rules - the host's choices are kept on their machine and used whenever they host
Long lists - the Server page shows players and blocked models ten at a time, latest blocked first, with a search box once there is more than one page
Flood protection - the host takes at most two picks a second from each player (the last one always lands) and refuses looks and model paths too long to be real, so no player can flood the server or everyone else with looks to load
Package vetting - the host checks every sbox.game pick against the same rules (a model, not tagged as a hand, arm, clothing or the citizen, enough bones) before anyone sees it, so a changed client can't wear what the window leaves out
Download limit - everyone here downloads each sbox.game model a player puts on, so each player other than the host brings in 3 new ones every 5 minutes, both set on the Server page (up to 50, over up to 60 minutes); every machine starts downloading a model as soon as the host takes it, so it is there when its player comes into view
Effects switch - the host can keep players' effects off; they stay with their looks and show again when it is turned back on
Effects budget - the host picks how much of their effects each player shows: Low (6 layers, 1 light and 150 sprites at once), Normal (16, 4 and 400, so any three presets fit), High (24, 6 and 1,000) or Custom (up to 32, 8 and 2,000); past it a player's last layers and lights wait and their sprites thin out, and nothing is taken from their look; the Effects tab marks what waits (Over this server's budget), and the preview shows a look as others see it
Effects switches - on the Server page the host can turn off effects from the Workshop, effects' lights, and shapes players drew (shown as plain circles and glows), mute any player's effects (Mute effects, in Players), and block one Workshop share and every copy of it (Block on this server, in its details), listed under Blocked from the Workshop to unblock; nothing is deleted, players are told why theirs are hidden, the host's own always show, and it is all kept with the server's rules
Scripts switch - the host can keep players' effects from running their scripts, and lower the longest script that runs (16 KB at most, down to 2 KB); such an effect shows what its script made when it was saved, nothing is deleted, and the host never runs anyone's scripts
# Preview
Zoom - scroll over the preview to zoom in towards the face
### Improved
# Player Model Selector window
Tab - the selector is a Custom Player Model tab in Sandbox's own window, after HUD and Physgun, shown while a selector is in the world, instead of a window next to Saves and Effects; it fits the screen it is on
Scrolling - the mouse wheel scrolls the panes at an easy pace, and every scrollbar can be dragged
Taller tab - a full page of models (20) and the Previous and Next buttons, centred under it, fit without scrolling (650 tall, was 600); a short screen still scrolls
Style - the tab follows Sandbox's own theme, dark or light, and keeps a copy of it for anything a Sandbox update renames or removes
Hearts - favoriting pops the heart and eases the model into the Favorites group
Icon - the entity's icon in the spawn menu and on sbox.game is the Player Model Selector logo instead of a render of the citizen
Invisible - the selector no longer stands in the world as a stone citizen with a body to bump into: it is invisible and solid to nothing; undo it or clean up to remove it
### Fixed
# Models
Stretched feet - a model with no ankle bone, like the GMod skeleton, no longer stretches its feet when it runs or jumps: the bend at the ankle used to land on a twist bone halfway down the shin
Detached heads - a model whose head isn't part of its skeleton's hierarchy (some Garry's Mod ports, Aishi Ayano among them) no longer wears its head floating beside the neck: the neck carries it
Floating feet - every model's planted foot now stands on the ground, whatever its leg length
Frozen legs - models whose body bones are outnumbered by hair and cloth bones walk again (Humanoid Retargeter fix)
Crumpled chests - models whose spine bends back on itself no longer twist the chest (Humanoid Retargeter fix)
Backward models - a model built facing the other way whose bone names can't be read (Elsa) no longer walks turned round, face to the camera: it faces where you go, its left arm on its left (Humanoid Retargeter fix)
Weapon effects - beams and muzzle flashes start from the model's own hand
Stacked bodies - a player no longer ends up wearing several copies of their model after many changes
Human shadows - a player wearing a model no longer casts the citizen's shadow under it; the shadow is the model's own, and the citizen's comes back with Default
Skateboard riders - riding a skateboard no longer turns you back into the citizen: your model rides, for everyone, dressed the same; any addon showing a player as a body of its own can have it too (README, Other addons)
Held weapons - a weapon in the model's hand no longer trails a frame behind it while you move or turn the camera; it sat where the hand was the frame before
# Preview
Stacked models - the preview no longer piles up copies of the model after a reload
### Known Issues
# Models
Old shaders - a package whose shader was built for an older s&box shows the error material in its bind pose
Source mounts - Half-Life 2 and Counter-Strike: Source need s&box's Source 1 mount, which not every install has
Mounted colors - mounted models take the whole-model tint only, not part colors
# View Model
Mounted arms - Counter-Strike, Half-Life and Deadlock have no first-person arms Sandbox's weapons can move, so View Model lists none from them

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
