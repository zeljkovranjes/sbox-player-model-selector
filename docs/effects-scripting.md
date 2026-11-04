Effect scripting
================

Effects, shapes and stickers can be written as small scripts. The language has
Lua's syntax and C#'s behaviour: `local`, `function ... end`, `if ... then ...
end`, `--` comments, and a library named like C# and s&box (`Math.Floor`,
`Color.Lerp`, `Vector3`), with every type checked when the script compiles.
Every effect in the Effects tab is a script, the stock presets included, and the
visual editor and the code editor edit the same script.

Every example on this page is run by the tests: it compiles with no errors, runs
within its budget, and each `local name = ... --> value` holds that value once it
has run. Examples run 1.5 seconds in (`time` is 1500), for element 2 of 5 (at
0.5, -0.25 on its shape), a quarter through its life, with the body running
sideways on the ground and the effect's place at (100, 200, 50) in the world. An
effect needs at least one layer, so an example that makes none is run with one
added.

<br>

## The language at a glance

```lua sticker
-- A comment runs to the end of its line; --[[ a block comment ]] can sit anywhere
local count = 3                    -- a number
local doubled = count * 2 --> 6
local name = "glow"                -- text
local bright = Color( 1, 0.8, 0.2 ) --> #ffcc33
local up = Vector3.Up * 2 --> Vector3( 0, 0, 2 )
local big = if doubled > 4 then "yes" else "no" --> "yes"

local function twice( x: number ): number
	return x * 2
end

local eight = twice( 4 ) --> 8
```

Statements are written one to a line (or separated by `;`). Blocks end with
`end`. Lists count from 0 and know their size, like C#:

```lua sticker
local odd = { 1, 3, 5, 7 }
local first = odd[ 0 ] --> 1
local n = odd.Count --> 4

local sum = 0
for k = 0, odd.Count - 1 do
	sum = sum + odd[ k ]
end

local total = sum --> 16
```

An item changes in place with `list[ i ] = value` (every variable holding the
list sees it); a list keeps its size. `List.Create( count, value )` makes one of
any size at the top of a script, for state its functions change as it runs:

```lua effect
local glow = Sprite()
local recent = List.Create( 4, 0 )
local next = 0

function OnLand( fallSpeed )
	recent[ next ] = fallSpeed
	next = (next + 1) % recent.Count
end

local slots = recent.Count --> 4
```

<br>

## Types

Every value has a type, worked out when the script compiles: `number`, `bool`,
`string`, `Color`, `Vector3`, `Key`, `Shape`, `Picture`, `Layer`, `Effect`,
`EmblemLayer`, a named choice (`Joint`, `Rhythm`, ...), a list `{T}` or a
function `(A, B) -> R`. A variable keeps the type of its first value. A
parameter of your own function is given its type:

```lua sticker
local function shade( c: Color, amount: number ): Color
	return c * amount
end

local dim = shade( Color.White, 0.5 ) --> #80808080
```

A mistake is an error at its line and column, before anything runs:

```console
3:18: Cannot use a Color as a number
5:1: 'Math.Flor' doesn't exist: did you mean 'Floor'?
```

Conditions must be bools: write `if n > 0 then`, not `if n then`. `and`, `or`
and `not` work on bools. There is no `nil`: every variable starts with a value.

Numbers are 64-bit, like C#'s `double`, and never go wrong: dividing by zero
gives 0, and nothing is ever NaN or infinite.

```lua sticker
local safe = 1 / 0 --> 0
local remainder = -7 % 3 --> -1
```

<br>

## Effect scripts

The top of an effect script runs once, when the effect is put on: it makes the
effect's layers with `Sprite()`, `Light()` or `Trail()` and sets their fields,
the same fields the visual editor shows. `Effect` is the effect itself.

```lua effect
Effect.Name = "Ember"
Effect.Joint = Joint.RightHand

local embers = Sprite()
embers.ComesFrom = ComesFrom.Sphere
embers.Spread = 2
embers.LooksLike = Picture.Spark
embers.Color = Color.Parse( "#ffb04a" )
embers.ColorEnd = Color.Parse( "#ff3d00" )
embers.Gravity = -30
embers.Amount = 25

local glow = Light()
glow.Color = Color.Parse( "#ff7a1f" )
glow.Size = 60
glow.Rhythm = Rhythm.Flicker
glow.RhythmSpeed = 6
glow.RhythmAmount = 0.3

local layers = 2
```

A layer's fields are listed under [Layer](#layer). Values out of a field's range
are held to it (a sprite is never bigger than 48, a light never brighter than 4).

A magic circle on the floor: the effect comes from the ground under the feet
(`Joint.Ground`), its circle of sprites lies flat (`Facing.Flat`, turned the way
the body faces) and each sprite lies flat on the ground too (`Align.Ground`), so
it reads as a circle drawn on the floor wherever the player walks or runs.

```lua effect
Effect.Name = "Magic circle"
Effect.Joint = Joint.Ground

-- Runes kept out all the time, spread evenly round a circle lying on the floor
local runes = Sprite()
runes.ComesFrom = ComesFrom.Shape
runes.Formation = Shapes.Circle
runes.Facing = Facing.Flat
runes.Spread = 28
runes.Offset = Vector3( 0, 0, 0.5 )
runes.LooksLike = Picture.Sparkle
runes.Align = Align.Ground
runes.Color = Color.Parse( "#7ad8ff" )
runes.Size = 4
runes.Amount = 0
runes.Keep = 16
runes.Follow = true

function runes.Particle( p )
	p.Rotation = t * 40 + p.Index * 22.5
	p.Alpha = 0.6 + 0.4 * Math.Sin( t * 3 + p.Index )
end

-- The ring itself, one sprite as wide as the circle, flat under the runes
local ring = Sprite()
ring.LooksLike = Shapes.Ring
ring.Align = Align.Ground
ring.Color = Color.Parse( "#7ad8ff" )
ring.Size = 60
ring.Amount = 0
ring.Keep = 1
ring.Follow = true

local glow = Light()
glow.Offset = Vector3( 0, 0, 4 )
glow.Color = Color.Parse( "#7ad8ff" )
glow.Size = 80
glow.Amount = 0.6

local flat = runes.Align --> Align.Ground
```

<br>

## Functions while it runs

Functions run while the effect shows. `OnUpdate( dt )` runs every frame; the
other events run when something happens to the player (see [Events](#events)).
Inside them the body's state is read from inputs like `speed`, `velocity`,
`grounded` and `crouching`, and a layer's live fields (its colour, sizes,
amount, speeds, offset, `Off`) can change.

```lua effect
local glow = Sprite()
glow.Color = Color.Parse( "#7af0ff" )

function OnUpdate( dt )
	glow.Size = 2 + speed / 100
	glow.Off = crouching
end
```

```lua effect
local dust = Sprite()
dust.LooksLike = Picture.Smoke
dust.Amount = 0
dust.Solid = true

function OnLand( fallSpeed )
	dust.Emit( Math.Min( fallSpeed / 20, 40 ) )
end
```

`OnShoot( shot )` gives every client the same shot, worked out from what it
already sees: where it left the muzzle, which way it went and what it hit.

```lua effect
local sparks = Sprite()
sparks.LooksLike = Picture.Spark
sparks.Amount = 0

function OnShoot( shot )
	if shot.Hit then
		sparks.EmitAt( shot.HitPosition + shot.HitNormal * 2, 12 )
	end
end
```

A layer's particle function runs for each of its sprites each frame; `p` is the
sprite:

```lua effect
local ring = Sprite()
ring.ComesFrom = ComesFrom.Ring
ring.Spread = 14
ring.Palette = { Color.Red, Color.Yellow, Color.Cyan }

function ring.Particle( p )
	local step = Math.Floor( t * 10 )
	p.Color = ring.Palette[ (step + p.Index) % ring.Palette.Count ]
	p.Alpha = 1 - p.Life
end
```

A function, event or particle run stops at its instruction budget (an endless
loop just stops) and goes no deeper than 32 calls. A run that stops changes
nothing more, and the effect keeps its own values.

<br>

## Parameters

`[Parameter]` on a variable at the top of a script makes it an option players
adjust in the Effects tab, without opening the code. The script's value is the
default; a player's choice is kept with the effect, held to the parameter's
range. The widget comes from the type: a slider for a number (a stepper for a
whole `Step`), a toggle for a bool, a colour picker, a palette editor for a list
of colours, a text box, a shape picker, a key picker, a dropdown for a choice
(like `Joint`) or `Choices`, three number rows for a `Vector3`. A shape picker
offers the stock shapes and the player's own shapes and stickers from My shapes
(made, used from the Workshop or forked): a chosen one of theirs is carried in
the look, so every player sees it without having it, and one that is gone from
both leaves the script's own value.

```lua effect
[Parameter( "Glow size", Min = 0.5, Max = 10, Step = 0.1, Group = "Look", Tip = "How big each glow is" )]
local glowSize = 2.6

[Parameter( "Pieces", Min = 1, Max = 50, Step = 1 )]
local pieces = 12

[Parameter]
local glowColor = Color.Parse( "#7af0ff" )

[Parameter( Choices = { "Calm", "Wild" } )]
local mood = "Calm"

local glow = Sprite()
glow.Size = glowSize
glow.Color = glowColor
glow.Speed = if mood == "Wild" then 40 else 5

local size = glow.Size --> 2.6
```

A bare `[Parameter]` is titled from its name (`glowColor` is "Glow color").

<br>

## Shapes, stickers and emblems

A script can only use shapes that exist: the 40 stock ones (`Shapes.Heart`,
`Shapes.Star5`, ...), the player's own (`Shapes.Mine( "COOL" )`,
`Stickers.Mine( "Gold skull" )`, `Emblems.Mine( "Clan" )`, checked when the
script compiles), or ones it draws itself.

A shape script draws its shape once, into `shape`: points joined into lines
(`EndLine`) or filled outlines (`EndOutline`), on a square from -1 to 1 each way,
at most 500 points.

```lua shape
-- A five-petal rose: r = cos( 5 angle ), traced once round
local petals = 5
local steps = 120
for k = 0, steps do
	local angle = k / steps * Math.PI * 2
	local r = Math.Cos( petals * angle ) * 0.9
	shape.Point( r * Math.Cos( angle ), r * Math.Sin( angle ) )
end

shape.EndLine()
local points = shape.PointCount --> 121
```

```lua shape
-- A star of any number of points
local tips = 7
for k = 0, tips * 2 - 1 do
	local angle = k / (tips * 2) * Math.PI * 2 + Math.PI / 2
	local r = if k % 2 == 0 then 0.95 else 0.4
	shape.Point( r * Math.Cos( angle ), r * Math.Sin( angle ) )
end

shape.EndOutline()
local points = shape.PointCount --> 14
```

```lua shape
-- Text along an arc, a letter at a time
local word = { "C", "O", "O", "L" }
for k = 0, word.Count - 1 do
	local angle = Math.PI * (0.75 - k / (word.Count - 1) * 0.5)
	shape.Letter( word[ k ], Math.Cos( angle ) * 0.7, Math.Sin( angle ) * 0.7, 0.45, angle * 180 / Math.PI - 90 )
end
```

Setting a shape's fill or outline makes it a sticker. Adding layers makes it an
emblem: a stack of shapes, each moved, turned, scaled, flipped and coloured.

```lua shape
local back = shape.AddLayer( Shapes.Hexagon )
back.Color = Color.Parse( "#20242c" )
back.OutlineColor = Color.Parse( "#ffd700" )
back.OutlineWidth = 0.08

local star = shape.AddLayer( Shapes.Star5 )
star.Scale = Vector3( 0.6, 0.6, 1 )
star.Color = Color.Parse( "#ffd700" )
star.Animation = Animation.Rainbow
local turned = star.Rotation --> 0
```

In an effect script, `Shapes.New( "name" )` makes a shape the same way, to use as
a layer's formation or look:

```lua effect
local heartRing = Shapes.New( "Heart ring" )
for k = 0, 5 do
	local angle = k / 6 * Math.PI * 2
	heartRing.Add( Shapes.Heart, Math.Cos( angle ) * 0.7, Math.Sin( angle ) * 0.7, 0.25 )
end

local sparks = Sprite()
sparks.ComesFrom = ComesFrom.Shape
sparks.Formation = heartRing
```

<br>

## Sticker scripts and colour modes

A sticker script runs for each element (a particle, a shape's point, an emblem
layer) every frame, and sets that element's `color`, `alpha`, `size`, `rotation`
and `offset`. It reads `i` (which element), `count`, `life`, the `palette` and
the time.

```lua sticker
-- Pulse the size and turn slowly
size = 1 + 0.1 * Math.Sin( t * 4 )
rotation = t * 30
local turned = rotation --> 45
```

The colour modes the visual editor offers are scripts like these. World at War's
clan tag colourings, exactly as the game does them:

```lua sticker
-- World at War's "rain": every 100 ms the palette moves one element along, so the colours scroll.
local step = Math.Floor( time / 100 )
color = palette[ (step + i) % palette.Count ]
local shown = color --> #ffff00
```

```lua sticker
-- World at War's "cycl": a colour wipes across the elements one step (100 ms) at a time; once it has covered
-- them all, the next colour starts.
local base = Math.Floor( time / (count * 100) ) % palette.Count
local position = Math.Floor( time / 100 ) % count
if i < position then
	color = palette[ base ]
else
	color = palette[ (base + palette.Count - 1) % palette.Count ]
end
```

```lua sticker
-- World at War's "cyln": a scanner eye sweeping back and forth across the elements; it is the highlight colour,
-- the rest are the other.
local position = Math.Floor( (Math.Cos( time * 0.004 ) + 1) * 0.5 * count )
color = if i == position then highlight else rest
```

<br>

## Weapon skin scripts

A weapon skin's moving colours are scripts too, run on your machine once a frame
for each different skin (however many players wear it) into a small table the
skin shader reads. A skin script runs once for each band along the weapon (`i`
of `count`), or at 64 places along it when it reads `along` (0 at the weapon's
back, 1 at its front), or once in all when it reads neither. It sets `color`
(painted over the weapon, keeping its light and dark) and `alpha` (how much it
covers), `shade` (what the weapon's own colour is multiplied by), `glow`,
`sheen`, and `scroll` and `spin` (how far its pattern has moved). Its `time`
and `t` run at the skin's own speed; its palette is in linear light, as the
shader lights it.

```lua skin
-- Rainbow: the colour wheel laid along the weapon, flowing forwards
color = Color.FromHsv( Math.Frac( along - time * 0.00025 ) * 360, 1, 1 )
alpha = 0.85
glow = color * 0.12
local covers = alpha --> 0.85
```

```lua skin
-- Pulse: the weapon's own colour brighter in the palette's first colour, and back, once a second
local beat = 0.5 - 0.5 * Math.Cos( t * Math.PI * 2 )
shade = Color.White + (palette[ 0 ] * 1.4 - Color.White) * beat
local strength = beat --> 1
```

```lua skin
-- A pattern scrolling half its size a second and turning once every four
scroll = t * 0.5
spin = t * 0.25
local moved = scroll --> 0.75
```

Every stock moving colour (Rainbow, Rain, Cycle, Scanner, Pulse, Glow,
Shimmer, Palette) and the pattern's scroll and spin are scripts like these.

You can write your own: in weapon mode, under the skin's Moving color, Write
your own opens the code editor on the colour picked (or Edit script on yours),
and the weapon in the preview follows as you type. The skin keeps it (16 KB at
most, counted with the look's other scripts), every player's machine runs it
the way it runs the stock ones, within the same budget, and where the host
switches scripts off the colour picked moves the weapon instead. Stock lets
your script go.

```lua skin
-- A heat haze: hot white at the muzzle cooling to deep red at the back, flickering as if it just fired
local heat = Math.Pow( along, 2 ) * (0.8 + 0.2 * Math.Sin( t * 30 + along * 12 ))
color = Color.Lerp( Color.Parse( "#3a0000" ), Color.Parse( "#fff2c0" ), heat )
alpha = 0.3 + 0.6 * along
glow = color * (heat * 0.8)
local front = along --> 0.25
```

<br>

## Weapon design scripts

A weapon's whole design is a script too: Edit as code in weapon mode opens it,
and the weapon panel and the code stay in step both ways. `Weapon` is the
weapon (its `Finish` and `Tint`), `Skin` what is painted over it; stickers,
props, hidden parts and effects are put on with `Weapon.AddSticker`,
`Weapon.AddProp`, `Weapon.HidePart` and `Weapon.AddEffect`, each held to the
same caps as the panel's (8 stickers, 8 props whose longest side is 16 units,
16 hidden parts, 4 effects of 8 layers). A design script is built once, when it
changes; it has no functions that run.

```lua weapon
-- Rainbow flowing skin: the colour wheel flowing along the weapon, twice the clan tags' pace, over carbon fibre
Skin.Pattern = Pattern.Carbon
Skin.Animation = SkinAnimation.Rainbow
Skin.Speed = 2
local pace = Skin.Speed --> 2
```

```lua weapon
-- Gold with a holo sticker: a gold finish, and a star printed on holo film on the left of the receiver
Weapon.Finish = Finish.Gold

local star = Weapon.AddSticker( Shapes.Star5 )
star.Position = Vector3( 4, 0, 6.5 )
star.Rotation = Vector3( 0, 0, -90 )
star.Size = 4
star.Finish = StickerFinish.Holo
star.Border = true
local printed = star.Finish --> StickerFinish.Holo
```

```lua weapon
-- Magazine replaced by a can: the can takes the magazine's place and follows it through a reload
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Part = "magazine"
can.Replaces = true
can.Scale = 0.8
local replaces = can.Replaces --> true
```

An effect on the weapon is a stock preset or one of your own, with its sliders
and `When` changed on what `Weapon.AddEffect` gives back:

```lua weapon
local tracer = Weapon.AddEffect( Effects.Preset( "Bullet tracer" ) )
tracer.Hue = 0.3
tracer.When = When.OnShoot
local shows = tracer.When --> When.OnShoot
```

<br>

## Keys

An effect can be shown by a key, for a moment each press. A player gets at most
one key start every 2 minutes, whatever the script says: the host holds everyone
to it, and scripts only ever see the presses it lets through.

```lua effect
Effect.Key = "g"

local flash = Sprite()
flash.Amount = 0

function OnKey( key )
	if key == "g" then
		flash.Emit( 50 )
	end
end
```

<br>

## Editing in both editors

The visual editor and the code editor edit the same script. A change in the
visual editor rewrites just the literal it comes from (a colour keeps the form it
was written in), or adds one line next to the layer's others, so your functions
and comments stay. A field the script works out (an expression, a loop, a
function) is shown as "set by script" with its line; one given by a parameter
is changed through the parameter.

Scripts are stored, sent and shared minified (no indentation, comments kept, a
`;` for each blank line between paragraphs) and opened formatted, in one style
for everyone.

<br>

## Limits

| What | Most |
|---|---|
| Script (minified) | 16 KB; 32 KB for all the scripts of one look |
| Line | 4096 characters |
| Text literal | 256 characters |
| Nesting (blocks, brackets) | 48 deep |
| Functions | 64 |
| Parameters | 32 |
| Layers | 8 per effect |
| Kept sprites | 100 per layer |
| Beam points | 64 per beam |
| Ghosts | 30 a second, 8 out at once, per layer |
| Pictures | 16 per layer |
| Shape points | 500 per shape |
| Emblem layers | 32 |
| Palette | 32 colours |
| Lists, once built | 16,384 values together |
| Building | 200,000 instructions |
| One run (frame, event, element) | 10,000 instructions |
| Calls | 32 deep |

A script over any limit is refused whole, with a message saying which; it never
runs in part. A script can only reach the library below: nothing of the engine,
files, the network, other players or the scene.

<br>

## Reference

<!-- reference:start -->

### Keywords

#### `local`

Makes a variable: local name = value (or local name: type = value). At the top of a script it lasts while the script does.

```lua sticker
local size = 2
local doubled = size * 2 --> 4
```

#### `function`

Makes a function: function name( a: number ): number ... end. Event handlers (OnJump) and layer.Particle are functions too.

```lua sticker
local function twice( x: number ): number
	return x * 2
end
local four = twice( 2 ) --> 4
```

#### `end`

Closes a function, if, for or while.

```lua sticker
local n = 0
if true then
	n = 1
end
local one = n --> 1
```

#### `if`

Runs a block when a condition is true: if c then ... elseif d then ... else ... end. As a value: if c then a else b.

```lua sticker
local big = if 5 > 3 then "yes" else "no" --> "yes"
```

#### `then`

Follows an if or elseif condition.

```lua sticker
local n = 0
if n == 0 then
	n = 2
end
local two = n --> 2
```

#### `elseif`

Another condition, tried when the ones before were false.

```lua sticker
local n = 5
local size = 0
if n < 3 then
	size = 1
elseif n < 10 then
	size = 2
end
local picked = size --> 2
```

#### `else`

What runs when no condition was true.

```lua sticker
local n = 5
local small = false
if n < 3 then
	small = true
else
	small = false
end
local answer = small --> false
```

#### `for`

Counts: for i = from, to[, step] do ... end, both ends included (step 1 unless given).

```lua sticker
local sum = 0
for k = 1, 4 do
	sum = sum + k
end
local total = sum --> 10
```

#### `do`

Starts the body of a for or while loop.

```lua sticker
local n = 0
while n < 3 do
	n = n + 1
end
local three = n --> 3
```

#### `while`

Repeats a block while a condition is true (stopped by the run's instruction budget).

```lua sticker
local n = 1
while n < 100 do
	n = n * 2
end
local power = n --> 128
```

#### `return`

Ends a function, giving back a value; it is the last statement of its block.

```lua sticker
local function half( x: number ): number
	return x / 2
end
local h = half( 3 ) --> 1.5
```

#### `break`

Leaves the loop it is in.

```lua sticker
local n = 0
for k = 0, 100 do
	if k == 5 then
		break
	end
	n = k
end
local last = n --> 4
```

#### `and`

True when both sides are (the right side only checked when the left is true).

```lua sticker
local both = true and false --> false
```

#### `or`

True when either side is (the right side only checked when the left is false).

```lua sticker
local either = true or false --> true
```

#### `not`

The opposite of a bool.

```lua sticker
local opposite = not true --> false
```

#### `true`

The bool for yes.

```lua sticker
local yes = true --> true
```

#### `false`

The bool for no.

```lua sticker
local no = false --> false
```

### Types

#### `number`

A number (64-bit, like C#'s double). Division by zero gives 0, and nothing is ever NaN or infinite.

```lua sticker
local half = 1 / 2 --> 0.5
```

#### `bool`

true or false. Conditions must be bools (no Lua truthiness): if n > 0, not if n.

```lua sticker
local more = 3 > 2 --> true
```

#### `string`

Text in quotes, "like this" or 'like this' (at most 256 characters). Text never changes while a script runs.

```lua sticker
local name = "Sparks" --> "Sparks"
```

#### `Color`

A colour: red, green, blue, alpha from 0 to 1. + - * / work on each part.

```lua sticker
local dim = Color.White * 0.5 --> #80808080
```

#### `Vector3`

Three numbers: a place or a direction. + - * / work like s&box's Vector3.

```lua sticker
local up2 = Vector3.Up * 2 --> Vector3( 0, 0, 2 )
```

#### `Key`

A key that can show an effect, written as text: "g", "f1", "mouse4". Any other text is an error.

```lua effect
Effect.Key = "g"
```

#### `Shape`

A standard shape (Shapes.Heart) or one the script drew (Shapes.New).

```lua sticker
local s: Shape = Shapes.Heart --> Shapes.Heart
```

#### `Picture`

What a sprite looks like: a curated picture (Picture.Flame) or any shape.

```lua sticker
local look: Picture = Picture.Flame --> Picture.Flame
```

#### `Layer`

A layer of the effect, made by Sprite(), Light(), Trail(), Beam() or Ghost().

```lua effect
local glow: Layer = Sprite()
```

#### `Effect`

An effect: this one (Effect), or one to include (Effects.Preset).

```lua effect
Effect.Include( Effects.Preset( "Hearts" ) )
```

#### `EmblemLayer`

One layer of an emblem, made by shape.AddLayer.

```lua effect
local badge = Shapes.New( "Badge" )
local star: EmblemLayer = badge.AddLayer( Shapes.Star5 )
```

#### `list`

{ a, b, c }: items of one type, numbered from 0, written {T} as a type. An item changes in place with list[ i ] = value (every variable holding the list sees it); a list keeps its size.

```lua sticker
local odd: {number} = { 1, 3, 5 }
odd[ 1 ] = 4
local second = odd[ 1 ] --> 4
```

#### `function`

A function as a value, written (A, B) -> R as a type.

```lua sticker
local function twice( x: number ): number
	return x * 2
end
local f: (number) -> number = twice
local six = f( 3 ) --> 6
```

### Parameters

#### `Parameter( [title: string] )`

Makes a top-level local an option players adjust in the Effects tab (the widget comes from its type); the script's value is the default.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `title` | string | any | What the option is called; the variable's name in words (glowSize: "Glow size") when left out (optional) |

```lua effect
[Parameter( "Glow size", Min = 0.5, Max = 10, Step = 0.1, Group = "Look", Tip = "How big each glow is" )]
local glowSize = 2.6

[Parameter]
local glowColor = Color.Parse( "#7af0ff" )
local size = glowSize --> 2.6
```

#### `Parameter.Min: number`

The least a number (or each part of a Vector3) can be set to; give Max too.

```lua effect
[Parameter( Min = 0, Max = 1 )]
local strength = 0.5
```

#### `Parameter.Max: number`

The most a number (or each part of a Vector3) can be set to; give Min too.

```lua effect
[Parameter( Min = -10, Max = 10 )]
local height = 2
```

#### `Parameter.Step: number`

How far one notch of its slider moves; a whole Step of 1 or more makes a whole-number stepper.

```lua effect
[Parameter( Min = 1, Max = 8, Step = 1 )]
local points = 5
```

#### `Parameter.Group: string`

The fold it is shown in, with the others of its group.

```lua effect
[Parameter( Group = "Look" )]
local tint = Color.White
```

#### `Parameter.Tip: string`

What it does, shown when hovering it.

```lua effect
[Parameter( Tip = "How fast it spins" )]
local spin = 1
```

#### `Parameter.Choices: {string}`

For text: the choices it is picked from, as a dropdown.

```lua effect
[Parameter( Choices = { "Spin", "Pulse" } )]
local motion = "Spin"
```

#### `Parameter.Order: number`

Where it is shown among the others (lowest first); they are in the script's order otherwise.

```lua effect
[Parameter( Order = 1 )]
local second = true
```

### Math

#### `Math`

Numbers: rounding, limits, trigonometry (radians), noise and repeatable randomness, named like C#'s Math.

#### `Math.PI: number`

Half a turn in radians: 3.14159...

```lua sticker
local pi = Math.PI --> 3.1416
```

#### `Math.Abs( x: number ): number`

x without its sign.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Any number |

```lua sticker
local a = Math.Abs( -3.5 ) --> 3.5
```

#### `Math.Sign( x: number ): number`

-1, 0 or 1: the sign of x.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Any number |

```lua sticker
local s = Math.Sign( -2 ) --> -1
```

#### `Math.Floor( x: number ): number`

The largest whole number at or below x.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Any number |

```lua sticker
local f = Math.Floor( 2.7 ) --> 2
local g = Math.Floor( -2.5 ) --> -3
```

#### `Math.Ceiling( x: number ): number`

The smallest whole number at or above x.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Any number |

```lua sticker
local c = Math.Ceiling( 2.1 ) --> 3
```

#### `Math.Round( x: number ): number`

x to the nearest whole number; halves go to the even one, like C#.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Any number |

```lua sticker
local r = Math.Round( 2.6 ) --> 3
local half = Math.Round( 2.5 ) --> 2
```

#### `Math.Frac( x: number ): number`

The part of x after its whole number (x - Math.Floor( x )), 0 up to 1.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Any number |

```lua sticker
local f = Math.Frac( 2.75 ) --> 0.75
```

#### `Math.Min( a: number, b: number ): number`

The smaller of a and b.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | number | any | A number |
| `b` | number | any | Another |

```lua sticker
local m = Math.Min( 3, 7 ) --> 3
```

#### `Math.Max( a: number, b: number ): number`

The larger of a and b.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | number | any | A number |
| `b` | number | any | Another |

```lua sticker
local m = Math.Max( 3, 7 ) --> 7
```

#### `Math.Clamp( x: number, min: number, max: number ): number`

x held between min and max (min when min is above max).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | The number |
| `min` | number | any | The least it may be |
| `max` | number | any | The most it may be |

```lua sticker
local c = Math.Clamp( 12, 0, 10 ) --> 10
```

#### `Math.Lerp( a: number, b: number, t: number ): number`

Part way from a to b: t = 0 is a, 1 is b (t held to 0..1, like s&box).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | number | any | Where it starts |
| `b` | number | any | Where it ends |
| `t` | number | 0 to 1 | How far along |

```lua sticker
local l = Math.Lerp( 0, 10, 0.25 ) --> 2.5
```

#### `Math.Sin( radians: number ): number`

The sine of an angle in radians.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `radians` | number | any | The angle |

```lua sticker
local s = Math.Sin( Math.PI / 2 ) --> 1
```

#### `Math.Cos( radians: number ): number`

The cosine of an angle in radians.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `radians` | number | any | The angle |

```lua sticker
local c = Math.Cos( 0 ) --> 1
```

#### `Math.Tan( radians: number ): number`

The tangent of an angle in radians.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `radians` | number | any | The angle |

```lua sticker
local t = Math.Tan( Math.PI / 4 ) --> 1
```

#### `Math.Atan2( y: number, x: number ): number`

The angle in radians from the X axis to the point (x, y).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `y` | number | any | Up |
| `x` | number | any | Across |

```lua sticker
local a = Math.Atan2( 1, 1 ) --> 0.7854
```

#### `Math.Sqrt( x: number ): number`

The square root of x; 0 for a negative x.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | 0 or more | A number |

```lua sticker
local s = Math.Sqrt( 16 ) --> 4
```

#### `Math.Pow( x: number, y: number ): number`

x to the power y; 0 where that isn't a number.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | The base |
| `y` | number | any | The power |

```lua sticker
local p = Math.Pow( 2, 10 ) --> 1024
```

#### `Math.Exp( x: number ): number`

e (2.71828...) to the power x: Math.Exp( -k * t ) dies away smoothly from 1.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | The power |

```lua sticker
local e = Math.Exp( 1 ) --> 2.7183
local fading = Math.Exp( -3 ) --> 0.0498
```

#### `Math.SmoothStep( from: number, to: number, x: number ): number`

0 at or below from, 1 at or above to, easing smoothly between (like HLSL's smoothstep).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `from` | number | any | Where it starts rising |
| `to` | number | any | Where it reaches 1 |
| `x` | number | any | The number |

```lua sticker
local half = Math.SmoothStep( 0, 1, 0.5 ) --> 0.5
local eased = Math.SmoothStep( 0, 1, 0.25 ) --> 0.1562
```

#### `Math.Noise( x: number, [y: number] ): number`

Smooth noise, 0 to 1, the same on every client: nearby inputs give nearby values.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Where along it |
| `y` | number | any | A second direction, for 2D noise (optional, 0) |

```lua sticker
local n = Math.Noise( 0.5, 2 ) --> 0.2582
```

#### `Math.Random( seed: number ): number`

A number from 0 up to 1 picked by seed: the same seed gives the same number on every client.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `seed` | number | any | Any number; the same one always gives the same result |

```lua sticker
local r = Math.Random( 7 ) --> 0.9843
```

### Color

#### `Color`

Colours as red, green, blue and alpha from 0 to 1, like s&box's Color.

#### `Color( r: number, g: number, b: number, [a: number] ): Color`

A colour from red, green, blue and (optional) alpha, each 0 to 1.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `r` | number | 0 to 1 | Red |
| `g` | number | 0 to 1 | Green |
| `b` | number | 0 to 1 | Blue |
| `a` | number | 0 to 1 | Alpha (opacity) (optional, 1) |

```lua sticker
local orange = Color( 1, 0.5, 0 ) --> #ff8000
```

#### `Color.Parse( hex: string ): Color`

A colour from text like "#ff3333" (or "#f33", or "#ff333380" with alpha).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `hex` | string | any | The colour as # and hex digits |

```lua sticker
local red = Color.Parse( "#ff3333" ) --> #ff3333
```

#### `Color.FromHsv( hue: number, saturation: number, value: number, [a: number] ): Color`

A colour from hue (degrees round the wheel), saturation and value.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `hue` | number | 0 to 360 | Round the wheel: 0 red, 120 green, 240 blue |
| `saturation` | number | 0 to 1 | 0 grey to 1 full |
| `value` | number | 0 to 1 | 0 black to 1 bright |
| `a` | number | 0 to 1 | Alpha (optional, 1) |

```lua sticker
local green = Color.FromHsv( 120, 1, 1 ) --> #00ff00
```

#### `Color.Lerp( a: Color, b: Color, t: number ): Color`

Part way from colour a to b (t held to 0..1).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | Color | any | Where it starts |
| `b` | Color | any | Where it ends |
| `t` | number | 0 to 1 | How far along |

```lua sticker
local grey = Color.Lerp( Color.Black, Color.White, 0.5 ) --> #808080
```

#### `Color.White: Color`

White.

```lua sticker
local named = Color.White --> #ffffff
```

#### `Color.Black: Color`

Black.

```lua sticker
local named = Color.Black --> #000000
```

#### `Color.Red: Color`

Red.

```lua sticker
local named = Color.Red --> #ff0000
```

#### `Color.Green: Color`

Green.

```lua sticker
local named = Color.Green --> #00ff00
```

#### `Color.Blue: Color`

Blue.

```lua sticker
local named = Color.Blue --> #0000ff
```

#### `Color.Yellow: Color`

Yellow.

```lua sticker
local named = Color.Yellow --> #ffff00
```

#### `Color.Cyan: Color`

Cyan.

```lua sticker
local named = Color.Cyan --> #00ffff
```

#### `Color.Magenta: Color`

Magenta.

```lua sticker
local named = Color.Magenta --> #ff00ff
```

#### `Color.Orange: Color`

Orange.

```lua sticker
local named = Color.Orange --> #ff8000
```

#### `Color.Gray: Color`

Gray.

```lua sticker
local named = Color.Gray --> #808080
```

#### `Color.Transparent: Color`

Transparent.

```lua sticker
local named = Color.Transparent --> #00000000
```

#### `Color.R: number  (read only)`

Its red, 0 to 1.

Set: read only.

```lua sticker
local orange = Color( 1, 0.5, 0 )
local part = orange.R --> 1
```

#### `Color.G: number  (read only)`

Its green, 0 to 1.

Set: read only.

```lua sticker
local orange = Color( 1, 0.5, 0 )
local part = orange.G --> 0.5
```

#### `Color.B: number  (read only)`

Its blue, 0 to 1.

Set: read only.

```lua sticker
local orange = Color( 1, 0.5, 0 )
local part = orange.B --> 0
```

#### `Color.A: number  (read only)`

Its alpha (opacity), 0 to 1.

Set: read only.

```lua sticker
local orange = Color( 1, 0.5, 0 )
local part = orange.A --> 1
```

### Vector3

#### `Vector3`

Positions and directions in three numbers, like s&box's Vector3: X forwards, Y left, Z up.

#### `Vector3( x: number, y: number, z: number ): Vector3`

A vector from x, y and z.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | any | Forwards |
| `y` | number | any | Left |
| `z` | number | any | Up |

```lua sticker
local v = Vector3( 1, 2, 3 ) --> Vector3( 1, 2, 3 )
```

#### `Vector3.Lerp( a: Vector3, b: Vector3, t: number ): Vector3`

Part way from a to b (t held to 0..1).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | Vector3 | any | Where it starts |
| `b` | Vector3 | any | Where it ends |
| `t` | number | 0 to 1 | How far along |

```lua sticker
local mid = Vector3.Lerp( Vector3.Zero, Vector3( 2, 4, 6 ), 0.5 ) --> Vector3( 1, 2, 3 )
```

#### `Vector3.Dot( a: Vector3, b: Vector3 ): number`

The dot product: how much a and b point the same way, times their lengths.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | Vector3 | any | A vector |
| `b` | Vector3 | any | Another |

```lua sticker
local d = Vector3.Dot( Vector3( 1, 2, 3 ), Vector3( 4, 5, 6 ) ) --> 32
```

#### `Vector3.Cross( a: Vector3, b: Vector3 ): Vector3`

The cross product: a vector square to both a and b.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | Vector3 | any | A vector |
| `b` | Vector3 | any | Another |

```lua sticker
local up = Vector3.Cross( Vector3.Forward, Vector3.Left ) --> Vector3( 0, 0, 1 )
```

#### `Vector3.Distance( a: Vector3, b: Vector3 ): number`

How far apart a and b are.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `a` | Vector3 | any | A point |
| `b` | Vector3 | any | Another |

```lua sticker
local d = Vector3.Distance( Vector3.Zero, Vector3( 3, 4, 0 ) ) --> 5
```

#### `Vector3.Zero: Vector3`

Zero: (0, 0, 0).

```lua sticker
local named = Vector3.Zero --> Vector3( 0, 0, 0 )
```

#### `Vector3.One: Vector3`

One: (1, 1, 1).

```lua sticker
local named = Vector3.One --> Vector3( 1, 1, 1 )
```

#### `Vector3.Forward: Vector3`

Forward: (1, 0, 0).

```lua sticker
local named = Vector3.Forward --> Vector3( 1, 0, 0 )
```

#### `Vector3.Backward: Vector3`

Backward: (-1, 0, 0).

```lua sticker
local named = Vector3.Backward --> Vector3( -1, 0, 0 )
```

#### `Vector3.Left: Vector3`

Left: (0, 1, 0).

```lua sticker
local named = Vector3.Left --> Vector3( 0, 1, 0 )
```

#### `Vector3.Right: Vector3`

Right: (0, -1, 0).

```lua sticker
local named = Vector3.Right --> Vector3( 0, -1, 0 )
```

#### `Vector3.Up: Vector3`

Up: (0, 0, 1).

```lua sticker
local named = Vector3.Up --> Vector3( 0, 0, 1 )
```

#### `Vector3.Down: Vector3`

Down: (0, 0, -1).

```lua sticker
local named = Vector3.Down --> Vector3( 0, 0, -1 )
```

#### `Vector3.X: number  (read only)`

Its X.

Set: read only.

```lua sticker
local v = Vector3( 1, 2, 3 )
local part = v.X --> 1
```

#### `Vector3.Y: number  (read only)`

Its Y.

Set: read only.

```lua sticker
local v = Vector3( 1, 2, 3 )
local part = v.Y --> 2
```

#### `Vector3.Z: number  (read only)`

Its Z.

Set: read only.

```lua sticker
local v = Vector3( 1, 2, 3 )
local part = v.Z --> 3
```

#### `Vector3.Length: number  (read only)`

How long it is.

Set: read only.

```lua sticker
local v = Vector3( 3, 4, 0 )
local length = v.Length --> 5
```

#### `Vector3.Normal: Vector3  (read only)`

The same direction, 1 long (zero stays zero).

Set: read only.

```lua sticker
local v = Vector3( 0, 0, 5 )
local n = v.Normal --> Vector3( 0, 0, 1 )
```

### Lists

#### `List`

Lists made to a size, to fill in and change item by item (list[ i ] = value).

#### `List.Create( count: number, value: any ): {any}`

A list of count copies of value (at most 256), made while the script is built: state to change item by item as it runs, like a path to follow.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `count` | number | 0 to 256 | How many items |
| `value` | any | any | What each item starts as; the list holds its type |

```lua effect
local path = List.Create( 3, Vector3.Zero )
path[ 1 ] = Vector3.Up
local second = path[ 1 ] --> Vector3( 0, 0, 1 )
local n = path.Count --> 3
```

#### `List.Count: number  (read only)`

How many items the list has; they are numbered from 0 to Count - 1.

Set: read only.

```lua sticker
local odd = { 1, 3, 5 }
local n = odd.Count --> 3
local first = odd[ 0 ] --> 1
```

### Effect

#### `Effect: Effect`

The effect this script makes: its name, key, anchor and sliders.

```lua effect
Effect.Name = "Sparks"
local named = Effect.Name --> "Sparks"
```

#### `Effects`

Whole effects to include in this one: the player's own ("My effects") and the presets.

#### `Effects.Mine( name: string ): Effect`

One of the player's saved effects, by name, to include (in a weapon design: to put on the weapon; one the design carries counts too).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `name` | string | any | Its name in My effects |

```lua effect
Effect.Include( Effects.Mine( "Fire hands" ) )
```

#### `Effects.Preset( name: string ): Effect`

One of the stock presets, by name, to include (in a weapon design: to put on the weapon).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `name` | string | any | Its name in the Effects tab |

```lua effect
Effect.Include( Effects.Preset( "Hearts" ) )
```

#### `Effect.Include( other: Effect )`

Adds every layer of another effect to this one (as long as there is room for them).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `other` | Effect | any | From Effects.Mine or Effects.Preset |

```lua effect
Effect.Include( Effects.Preset( "Hearts" ) )
```

#### `Effect.Name: string`

What the effect is called.

Set: anywhere.

```lua effect
Effect.Name = "Sparks"
local value = Effect.Name --> "Sparks"
```

#### `Effect.Key: Key`

The key that shows it for a moment each press (one key start every 2 minutes per player).

Set: while the effect is built (at the top of the script).

```lua effect
Effect.Key = "g"
local value = Effect.Key --> "g"
```

#### `Effect.Off: bool`

Whether it is switched off: kept with the look, not shown.

Set: anywhere.

```lua effect
Effect.Off = true
local value = Effect.Off --> true
```

#### `Effect.Anchor: Anchor`

What it comes from: a joint, an item worn (Effect.Item) or the weapon held.

Set: while the effect is built (at the top of the script).

```lua effect
Effect.Anchor = Anchor.HeldWeapon
local value = Effect.Anchor --> Anchor.HeldWeapon
```

#### `Effect.Joint: Joint`

The joint it comes from (for an item or a weapon, the joint used while that isn't there).

Set: while the effect is built (at the top of the script).

```lua effect
Effect.Joint = Joint.Chest
local value = Effect.Joint --> Joint.Chest
```

#### `Effect.Item: string`

The worn item it comes from, for Anchor.Item.

Set: while the effect is built (at the top of the script).

```lua effect
Effect.Item = "models/hat.vmdl"
local value = Effect.Item --> "models/hat.vmdl"
```

#### `Effect.Offset: Vector3  (-24 to 24)`

How far it is moved from where it comes from: forwards, left and up.

Set: while the effect is built (at the top of the script).

```lua effect
Effect.Offset = Vector3( 0, 0, 4 )
local value = Effect.Offset --> Vector3( 0, 0, 4 )
```

#### `Effect.Hue: number  (0 to 1)`

The colour slider: how far round the colour wheel every colour is turned.

Set: anywhere.

```lua effect
Effect.Hue = 0.5
local value = Effect.Hue --> 0.5
```

#### `Effect.Size: number  (-1 to 1)`

The size slider: -1 half, 0 as made, 1 twice the size.

Set: anywhere.

```lua effect
Effect.Size = 0.5
local value = Effect.Size --> 0.5
```

#### `Effect.Amount: number  (-1 to 1)`

The amount slider: -1 a quarter, 1 four times the sprites (and light).

Set: anywhere.

```lua effect
Effect.Amount = -0.5
local value = Effect.Amount --> -0.5
```

#### `Effect.Speed: number  (-1 to 1)`

The speed slider: -1 half, 1 twice as fast.

Set: anywhere.

```lua effect
Effect.Speed = 0.25
local value = Effect.Speed --> 0.25
```

### Layer

#### `Sprite(): Layer`

Adds a layer of sprites (flames, sparks, hearts) to the effect: at most 8 layers.

```lua effect
local glow = Sprite()
local kind = glow.Kind --> Kind.Sprite
```

#### `Light(): Layer`

Adds a light to the effect: its Size is how far it reaches, its Amount how bright.

```lua effect
local lamp = Light()
local kind = lamp.Kind --> Kind.Light
```

#### `Trail(): Layer`

Adds a trail left behind as its place moves.

```lua effect
local streak = Trail()
local kind = streak.Kind --> Kind.Trail
```

#### `Beam(): Layer`

Adds a beam: a glowing ribbon through its points in order (Keep of them, 16 unless set, 2 to 64), laid evenly along where it comes from (to ToJoint for ComesFrom.Line) and moved by its particle function: bolts, lasers, ropes. Size is its width; its colour runs from Color to ColorEnd along it.

```lua effect
local bolt = Beam()
bolt.ComesFrom = ComesFrom.Line
bolt.ToJoint = Joint.RightHand
local kind = bolt.Kind --> Kind.Beam
local points = bolt.Keep --> 16
```

#### `Ghost(): Layer`

Adds ghosts: see-through copies of the body (or of the item or weapon its effect comes from), each the pose as it was, tinted Color fading to ColorEnd and gone after Lifetime: afterimages. Amount is how many a second (at most 30); at most 8 are out at once.

```lua effect
local afterimage = Ghost()
afterimage.Color = Color.Parse( "#7ad8ff" )
afterimage.Amount = 12
afterimage.Lifetime = 0.4
local kind = afterimage.Kind --> Kind.Ghost
```

#### `Layer.Emit( count: number )`

Sends out count sprites of the layer at once, where it is (within the budget).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `count` | number | 0 to 100 | How many |

```lua effect
local sparks = Sprite()
function OnJump()
	sparks.Emit( 20 )
end
```

#### `Layer.EmitAt( position: Vector3, count: number )`

Sends out count sprites of the layer at a place in the world (a shot's HitPosition, say).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `position` | Vector3 | any | Where, in the world |
| `count` | number | 0 to 100 | How many |

```lua effect
local sparks = Sprite()
function OnShoot( shot )
	if shot.Hit then
		sparks.EmitAt( shot.HitPosition, 12 )
	end
end
```

#### `Layer.Kind: Kind  (read only)`

What the layer is: Sprite(), Light(), Trail(), Beam() or Ghost() made it.

Set: read only.

```lua effect
local glow = Sprite()
local value = glow.Kind --> Kind.Sprite
```

#### `Layer.Off: bool`

Whether it is switched off: kept, but not shown.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Off = true
local value = glow.Off --> true
```

#### `Layer.Joint: Joint`

The joint it comes from, in place of its effect's.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Joint = Joint.LeftHand
local value = glow.Joint --> Joint.LeftHand
```

#### `Layer.ComesFrom: ComesFrom`

Where its sprites come from around its place ("Comes from").

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.ComesFrom = ComesFrom.Sphere
local value = glow.ComesFrom --> ComesFrom.Sphere
```

#### `Layer.Spread: number  (0 to 48)`

How far that reaches (its radius).

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Spread = 6
local value = glow.Spread --> 6
```

#### `Layer.Offset: Vector3  (-24 to 24)`

How far it sits from its joint: forwards (X), to the body's left (Y) and up (Z).

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Offset = Vector3( 3, 0, 1 )
local value = glow.Offset --> Vector3( 3, 0, 1 )
```

#### `Layer.Facing: Facing`

The way it faces: where a cone opens, a line runs, a shape looks.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Facing = Facing.Up
local value = glow.Facing --> Facing.Up
```

#### `Layer.Angle: number  (0 to 45)`

How wide a cone opens, in degrees from its middle.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Angle = 30
local value = glow.Angle --> 30
```

#### `Layer.ToJoint: Joint`

The joint a line runs to (ComesFrom.Line).

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.ToJoint = Joint.RightHand
local value = glow.ToJoint --> Joint.RightHand
```

#### `Layer.Formation: Shape`

The shape sprites come from (ComesFrom.Shape): a standard one or one the script made.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Formation = Shapes.Heart
local value = glow.Formation --> Shapes.Heart
```

#### `Layer.Fill: bool`

Whether sprites come from all over the shape rather than along its outline.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Fill = true
local value = glow.Fill --> true
```

#### `Layer.LooksLike: Picture`

What each sprite looks like ("Looks like"): a picture or a shape.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.LooksLike = Picture.Flame
local value = glow.LooksLike --> Picture.Flame
```

#### `Layer.Color: Color  (0 to 1)`

Its colour: a sprite's when it appears, the light's, a trail's where it starts.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Color = Color.Parse( "#ff8800" )
local value = glow.Color --> #ff8800
```

#### `Layer.ColorEnd: Color  (0 to 1)`

A sprite's colour as it fades away (a trail's at its end).

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.ColorEnd = Color.Red
local value = glow.ColorEnd --> #ff0000
```

#### `Layer.ColorMode: ColorMode`

How its sprites are coloured: fading, a World at War cycle over the palette, or the palette in turn.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.ColorMode = ColorMode.Rain
local value = glow.ColorMode --> ColorMode.Rain
```

#### `Layer.Palette: {Color}`

The colours a colour cycle goes through; the game's five when none are given.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Palette = { Color.Red, Color.Blue }
local value = glow.Palette --> { #ff0000, #0000ff }
```

#### `Layer.Size: number  (0.1 to 48)`

A sprite's size when it appears, how far a light reaches, how wide a trail starts.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Size = 3
local value = glow.Size --> 3
```

#### `Layer.SizeEnd: number  (0.1 to 48)`

A sprite's size as it fades away (a trail's width at its end).

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.SizeEnd = 0.5
local value = glow.SizeEnd --> 0.5
```

#### `Layer.Speed: number  (0 to 200)`

How fast sprites leave their shape, outwards.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Speed = 20
local value = glow.Speed --> 20
```

#### `Layer.Gravity: number  (-400 to 400)`

How hard sprites fall; negative rises.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Gravity = -40
local value = glow.Gravity --> -40
```

#### `Layer.Lifetime: number  (0.1 to 5)`

How long a sprite lives, or a trail stays behind, in seconds.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Lifetime = 1.5
local value = glow.Lifetime --> 1.5
```

#### `Layer.Amount: number  (0 to 100)`

How many sprites appear a second, or how bright a light is.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.Amount = 25
local value = glow.Amount --> 25
```

#### `Layer.Follow: bool`

Whether sprites move with the body once out, rather than staying where they appeared.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Follow = true
local value = glow.Follow --> true
```

#### `Layer.Solid: bool`

Whether sprites cover what is behind them (smoke, hearts) instead of glowing over it.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Solid = true
local value = glow.Solid --> true
```

#### `Layer.Rhythm: Rhythm`

How it changes over time.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Rhythm = Rhythm.Pulse
local value = glow.Rhythm --> Rhythm.Pulse
```

#### `Layer.RhythmSpeed: number  (0 to 10)`

How many times a second the rhythm beats (turns, for Spin).

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.RhythmSpeed = 2
local value = glow.RhythmSpeed --> 2
```

#### `Layer.RhythmAmount: number  (0 to 1)`

How much the rhythm changes it.

Set: while it is built and while it runs (in its functions).

```lua effect
local glow = Sprite()
glow.RhythmAmount = 0.5
local value = glow.RhythmAmount --> 0.5
```

#### `Layer.When: When`

When it shows: always, while something is so, or for a moment when something happens.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.When = When.OnLand
local value = glow.When --> When.OnLand
```

#### `Layer.Burst: number  (0 to 100)`

How many sprites appear at once when its moment comes (When.On...).

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Burst = 30
local value = glow.Burst --> 30
```

#### `Layer.ShowFor: number  (0 to 5)`

How long it shows after its moment, in seconds.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.ShowFor = 0.5
local value = glow.ShowFor --> 0.5
```

#### `Layer.FadeWithSpeed: bool`

Whether it shows only as much as its place moves (none standing still).

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.FadeWithSpeed = true
local value = glow.FadeWithSpeed --> true
```

#### `Layer.Keep: number  (0 to 100)`

How many sprites it keeps out all the time (at most 100), each with its own p.Index from 0, spread evenly along where they come from in that order and never fading with age (their Life loops every Lifetime): a formation its particle function moves, like runes in a ring or orbs following. 0: sprites come and go by Amount and Lifetime. For a beam, the points it is drawn through.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Keep = 12
local value = glow.Keep --> 12
```

#### `Layer.Align: Align`

Which way its sprites face: the camera (most do); the plane of a swing (a slash lies along the sword's sweep); flat on the ground, turned the way the body faces (footprints); the way the layer faces (Facing: a portal's disc); or out from its place, like tiles on a ball. p.Rotation turns them within that plane.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Align = Align.Ground
local value = glow.Align --> Align.Ground
```

#### `Layer.Pictures: {Picture}`

The pictures its sprites take, one each (at most 16): in turn as they appear, or as its particle function picks with p.Frame. Pictures or shapes; LooksLike when there are none.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Pictures = { Picture.Heart, Shapes.Star5 }
local value = glow.Pictures --> { Picture.Heart, Shapes.Star5 }
```

#### `Layer.Drag: number  (0 to 1)`

While its sprites follow the body (Follow), how far they lag behind it: 0 right on it, 1 left behind as a trail is, between a little behind (snow that stays on a runner yet drifts).

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Drag = 0.4
local value = glow.Drag --> 0.4
```

#### `Layer.AtHit: bool`

Whether it shows where the last shot or swing hit, facing out of what it hit (impact sparks), rather than at its place: for a layer shown When.OnShoot or When.OnHit.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.AtHit = true
local value = glow.AtHit --> true
```

#### `Layer.Sweep: number  (-720 to 720)`

How far each sprite turns over its life, in degrees, negative the other way: a slash sweeping round with the swing.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.Sweep = 120
local value = glow.Sweep --> 120
```

#### `Layer.SlashArc: number  (30 to 330)`

How far round a slash (Picture.Slash) reaches, in degrees.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.SlashArc = 160
local value = glow.SlashArc --> 160
```

#### `Layer.SlashThickness: number  (0.05 to 0.6)`

How thick a slash (Picture.Slash) is at its middle, a share of its radius.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
glow.SlashThickness = 0.3
local value = glow.SlashThickness --> 0.3
```

#### `Layer.Particle: (Particle) -> nothing`

The function run for each of its sprites (a beam's points) each frame: function layer.Particle( p ) ... end.

Set: while the effect is built (at the top of the script).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Alpha = 1 - p.Life
end
```

#### `Picture`

The curated pictures a sprite can look like (a shape can be one too).

#### Pictures

`Picture.Glow`, `Picture.Spark`, `Picture.Flame`, `Picture.Smoke`, `Picture.Sparkle`, `Picture.Heart`, `Picture.Snowflake`, `Picture.Droplet`, `Picture.Slash`

### Particle

#### `Particle.Index: number  (read only)`

Which sprite this is, from 0, in the order they appeared.

Set: read only.

```lua effect
local glow = Sprite()
function glow.Particle( p )
	local even = p.Index % 2 == 0
end
```

#### `Particle.Count: number  (read only)`

How many sprites the layer has out.

Set: read only.

```lua effect
local glow = Sprite()
function glow.Particle( p )
	local half = p.Count / 2
end
```

#### `Particle.Age: number  (read only)`

How long the sprite has been out, in seconds.

Set: read only.

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Size = 1 + p.Age
end
```

#### `Particle.Life: number  (read only)`

How far through its life it is, 0 to 1.

Set: read only.

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Alpha = 1 - p.Life
end
```

#### `Particle.Seed: number  (read only)`

A number from 0 up to 1 picked when it appears (by each client for itself), the same all its life: to make each one different (Math.Random( p.Seed + 1 ) for another).

Set: read only.

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Rotation = p.Seed * 360
end
```

#### `Particle.Origin: Vector3  (read only)`

Where it appeared on its layer's shape, from the layer's place, the way that faces: forwards, left and up (a joint faces the way the body does; an item or weapon its own way).

Set: read only.

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Offset = p.Origin * Math.Sin( t )
end
```

#### `Particle.Color: Color  (0 to 1)`

Its colour now.

Set: while it runs (in its functions).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Color = Color.FromHsv( p.Index * 30, 1, 1 )
end
```

#### `Particle.Alpha: number  (0 to 1)`

How solid it is now, 0 to 1.

Set: while it runs (in its functions).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Alpha = 0.5
end
```

#### `Particle.Size: number  (0 to 48)`

How big it is now, times its own size.

Set: while it runs (in its functions).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Size = 2
end
```

#### `Particle.Rotation: number  (-3600 to 3600)`

How far it is turned now, in degrees.

Set: while it runs (in its functions).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Rotation = t * 90
end
```

#### `Particle.Offset: Vector3  (-96 to 96)`

How far it is moved from where it would be, the way its layer's place faces: forwards, left and up (a joint faces the way the body does; an item or weapon its own way). Up to 96 each way.

Set: while it runs (in its functions).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Offset = Vector3( 0, 0, Math.Sin( t ) )
end
```

#### `Particle.Frame: number  (0 to 15)`

Which of its layer's Pictures it shows now, from 0, a fraction rounded down, past the last showing the last (they take them in turn unless this picks).

Set: while it runs (in its functions).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Frame = Math.Floor( p.Seed * 4 + t * 2 ) % 4
end
```

#### `Particle.Position: Vector3`

Where it is in the world; set it to put it anywhere within 400 units of the effect's place each way (its Offset still moves it from there).

Set: while it runs (in its functions).

```lua effect
local glow = Sprite()
function glow.Particle( p )
	p.Position = anchorPosition + Vector3( 0, 0, 40 + p.Index * 4 )
end
```

### Shape

#### `Shape.FillColor: Color  (0 to 1)`

A sticker's fill colour (setting any sticker property makes the shape a sticker).

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.FillColor = Color.Red
local value = badge.FillColor --> #ff0000
```

#### `Shape.OutlineColor: Color  (0 to 1)`

A sticker's outline colour.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.OutlineColor = Color.Black
local value = badge.OutlineColor --> #000000
```

#### `Shape.OutlineWidth: number  (0 to 0.2)`

How wide a sticker's outline is, on the shape's square (2 across).

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.OutlineWidth = 0.1
local value = badge.OutlineWidth --> 0.1
```

#### `Shape.Animation: Animation`

How a sticker's fill colour moves.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.Animation = Animation.Rain
local value = badge.Animation --> Animation.Rain
```

#### `Shape.Palette: {Color}`

The colours a sticker's colour cycle goes through.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.Palette = { Color.Red, Color.Yellow }
local value = badge.Palette --> { #ff0000, #ffff00 }
```

#### `Shape.AnimationSpeed: number  (0 to 10)`

How fast a sticker's animation runs: 1 is the game's own pace.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.AnimationSpeed = 2
local value = badge.AnimationSpeed --> 2
```

#### `Shape.Mirror: bool`

Whether an emblem (a shape of layers) is drawn again flipped left to right, as one half of a symmetric picture.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.Mirror = true
local value = badge.Mirror --> true
```

#### `Shape.Finish: StickerFinish`

What a sticker is printed on: paper, holo, foil, glitter, gold, or lenticular (two looks by the angle, see Side).

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
badge.Finish = StickerFinish.Holo
local value = badge.Finish --> StickerFinish.Holo
```

#### `Shape.PointCount: number  (read only)`

How many points the shape has so far (at most 500).

Set: read only.

```lua effect
local badge = Shapes.New( "Badge" )
local value = badge.PointCount --> 0
```

#### `Shapes`

Shapes: the 40 stock ones, the player's own (shapes, stickers, emblems), and new ones the script draws.

#### `shape: Shape`

In a shape script, the shape it makes.

```lua shape
shape.Point( 0, 0 )
shape.Point( 1, 1 )
shape.EndLine()
local points = shape.PointCount --> 2
```

#### `Shapes.New( name: string ): Shape`

A new empty shape to draw into, by name.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `name` | string | any | What it is called |

```lua effect
local badge = Shapes.New( "Badge" )
local points = badge.PointCount --> 0
```

#### `Shapes.Text( text: string ): Shape`

Text as a shape, in single lines the same on every machine (letters, digits, common punctuation; up to 24).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `text` | string | any | What it says |

```lua effect
local word = Shapes.Text( "COOL" )
```

#### `Shapes.Mine( name: string ): Shape`

One of the player's own shapes (My shapes), by name; checked when the script compiles.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `name` | string | any | Its name in My shapes |

```lua effect
local logo = Shapes.Mine( "COOL" )
```

#### `Stickers`

The player's own stickers (shapes with colours), from My shapes.

#### `Stickers.Mine( name: string ): Shape`

One of the player's own stickers, by name; checked when the script compiles.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `name` | string | any | Its name in My shapes |

```lua effect
local skull = Stickers.Mine( "Gold skull" )
```

#### `Emblems`

The player's own emblems (stacks of shape layers), from My shapes.

#### `Emblems.Mine( name: string ): Shape`

One of the player's own emblems, by name; checked when the script compiles.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `name` | string | any | Its name in My shapes |

```lua effect
local clan = Emblems.Mine( "Clan" )
```

#### `Shape.Point( x: number, y: number )`

Adds a point to the line being drawn (the square is -1 to 1 each way, y up).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `x` | number | -1 to 1 | Across |
| `y` | number | -1 to 1 | Up |

```lua effect
local mark = Shapes.New( "Mark" )
mark.Point( -1, 0 )
mark.Point( 1, 0 )
mark.EndLine()
local points = mark.PointCount --> 2
```

#### `Shape.Points( xy: {number} )`

Adds points to the line being drawn, from a list of x, y pairs.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `xy` | {number} | any | x, y, x, y, ... |

```lua effect
local box = Shapes.New( "Box" )
box.Points( { -1, -1, 1, -1, 1, 1, -1, 1 } )
box.EndOutline()
local points = box.PointCount --> 4
```

#### `Shape.EndLine()`

Finishes the line being drawn as an open line.

```lua effect
local mark = Shapes.New( "Mark" )
mark.Points( { -1, 0, 1, 0 } )
mark.EndLine()
```

#### `Shape.EndOutline()`

Finishes the line being drawn as a closed outline, filled in.

```lua effect
local tri = Shapes.New( "Tri" )
tri.Points( { 0, 1, 1, -1, -1, -1 } )
tri.EndOutline()
```

#### `Shape.Letter( text: string, x: number, y: number, size: number, [angle: number] )`

Draws text at a place, size and angle (to put letters along an arc, say).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `text` | string | any | What it says |
| `x` | number | -1 to 1 | Its middle, across |
| `y` | number | -1 to 1 | Its middle, up |
| `size` | number | any | How tall: 2 fills the square |
| `angle` | number | any | Turned, in degrees (optional, 0) |

```lua effect
local ring = Shapes.New( "Ring text" )
ring.Letter( "A", 0, 0.5, 0.4, 0 )
```

#### `Shape.Add( other: Shape, [x: number], [y: number], [size: number], [angle: number] )`

Draws another shape into this one at a place, size and angle (combining shapes).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `other` | Shape | any | The shape to draw |
| `x` | number | -1 to 1 | Its middle, across (optional, 0) |
| `y` | number | -1 to 1 | Its middle, up (optional, 0) |
| `size` | number | any | 1 is its own size (optional, 1) |
| `angle` | number | any | Turned, in degrees (optional, 0) |

```lua effect
local pair = Shapes.New( "Pair" )
pair.Add( Shapes.Heart, -0.5, 0, 0.5 )
pair.Add( Shapes.Heart, 0.5, 0, 0.5 )
```

#### `Shape.Fit()`

Moves the shape to the middle and sizes it to fill the square, keeping its proportions.

```lua effect
local small = Shapes.New( "Small" )
small.Points( { 0, 0, 0.1, 0.1 } )
small.EndLine()
small.Fit()
```

#### `Shape.Fade( from: number, to: number )`

As a sprite's picture, the shape fades round its middle: nothing at angle from, growing smoothly to all of it at angle to (anticlockwise when to is the larger, else clockwise): a slash bright at its front, gone at its tail. Past to it stays whole; behind from, clear.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `from` | number | any | Where it starts from nothing, in degrees anticlockwise from the right |
| `to` | number | at most a turn from from | Where it is whole |

```lua effect
local swoosh = Shapes.New( "Swoosh" )
swoosh.Add( Shapes.Crescent )
swoosh.Fade( -60, 90 )
```

#### Stock shapes

`Shapes.Circle`, `Shapes.Ellipse`, `Shapes.Semicircle`, `Shapes.Arc`, `Shapes.Ring`, `Shapes.Crescent`, `Shapes.Triangle`, `Shapes.RightTriangle`, `Shapes.Square`, `Shapes.Rectangle`, `Shapes.RoundedRectangle`, `Shapes.Diamond`, `Shapes.Parallelogram`, `Shapes.Trapezoid`, `Shapes.Kite`, `Shapes.Pentagon`, `Shapes.Hexagon`, `Shapes.Octagon`, `Shapes.Star4`, `Shapes.Star5`, `Shapes.Star6`, `Shapes.Star8`, `Shapes.Plus`, `Shapes.Cross`, `Shapes.Heart`, `Shapes.Teardrop`, `Shapes.Line`, `Shapes.SineWave`, `Shapes.Zigzag`, `Shapes.Spiral`, `Shapes.Infinity`, `Shapes.Helix`, `Shapes.Sphere`, `Shapes.Hemisphere`, `Shapes.Cube`, `Shapes.Cylinder`, `Shapes.Cone`, `Shapes.Pyramid`, `Shapes.Torus`, `Shapes.Capsule`

### Emblem

#### `EmblemLayer.Shape: Shape`

The shape the emblem layer draws.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Shape = Shapes.Heart
local value = star.Shape --> Shapes.Heart
```

#### `EmblemLayer.Position: Vector3  (-1 to 1)`

Where its middle is on the emblem's square (X right, Y up, -1 to 1; Z unused).

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Position = Vector3( 0.5, -0.25, 0 )
local value = star.Position --> Vector3( 0.5, -0.25, 0 )
```

#### `EmblemLayer.Rotation: number  (-360 to 360)`

How far it is turned, in degrees, anticlockwise.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Rotation = 45
local value = star.Rotation --> 45
```

#### `EmblemLayer.Scale: Vector3  (0.01 to 4)`

How big it is across (X) and up (Y): 1 fills the square; Z unused.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Scale = Vector3( 0.5, 0.8, 1 )
local value = star.Scale --> Vector3( 0.5, 0.8, 1 )
```

#### `EmblemLayer.FlipX: bool`

Whether it is flipped left to right.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.FlipX = true
local value = star.FlipX --> true
```

#### `EmblemLayer.FlipY: bool`

Whether it is flipped top to bottom.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.FlipY = true
local value = star.FlipY --> true
```

#### `EmblemLayer.Color: Color  (0 to 1)`

Its fill colour.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Color = Color.Yellow
local value = star.Color --> #ffff00
```

#### `EmblemLayer.Opacity: number  (0 to 1)`

How much it covers what is below it: 0 not at all, 1 fully.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Opacity = 0.75
local value = star.Opacity --> 0.75
```

#### `EmblemLayer.OutlineColor: Color  (0 to 1)`

Its outline's colour.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.OutlineColor = Color.Black
local value = star.OutlineColor --> #000000
```

#### `EmblemLayer.OutlineWidth: number  (0 to 0.2)`

How wide its outline is; 0 for none.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.OutlineWidth = 0.05
local value = star.OutlineWidth --> 0.05
```

#### `EmblemLayer.Animation: Animation`

How its colour moves.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Animation = Animation.Rainbow
local value = star.Animation --> Animation.Rainbow
```

#### `EmblemLayer.Palette: {Color}`

The colours its colour cycle goes through.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Palette = { Color.Cyan, Color.Magenta }
local value = star.Palette --> { #00ffff, #ff00ff }
```

#### `EmblemLayer.AnimationSpeed: number  (0 to 10)`

How fast its animation runs: 1 is the game's own pace.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.AnimationSpeed = 0.5
local value = star.AnimationSpeed --> 0.5
```

#### `EmblemLayer.Hidden: bool`

Whether it is hidden: kept, not drawn.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Hidden = true
local value = star.Hidden --> true
```

#### `EmblemLayer.Eraser: bool`

Whether it is an eraser: it draws nothing and cuts its shape out of every layer below it.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Eraser = true
local value = star.Eraser --> true
```

#### `EmblemLayer.Side: number  (0 to 2)`

Which look of a lenticular sticker it is in: 0 both, 1 the first, 2 the second.

Set: while the shape or effect is built.

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
star.Side = 2
local value = star.Side --> 2
```

#### `Shape.AddLayer( shape: Shape ): EmblemLayer`

Adds a layer to the shape, making it an emblem: a stack of shapes, each moved, turned, scaled and coloured (at most 32).

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `shape` | Shape | any | What the layer draws |

```lua effect
local badge = Shapes.New( "Badge" )
local star = badge.AddLayer( Shapes.Star5 )
local drawn = star.Shape --> Shapes.Star5
```

### Weapon

#### `Weapon.Finish: Finish`

What the weapon is made of: its own materials (Finish.None) or a stock finish, under its tint and skin.

Set: in a weapon design script.

```lua weapon
Weapon.Finish = Finish.Gold
local value = Weapon.Finish --> Finish.Gold
```

#### `Weapon.Tint: Color  (0 to 1)`

The colour the weapon is tinted (white when it isn't).

Set: in a weapon design script.

```lua weapon
Weapon.Tint = Color.Parse( "#ff8800" )
local value = Weapon.Tint --> #ff8800
```

#### `WeaponSkin.Pattern: Pattern`

What is laid over the weapon from every side (no UVs needed).

Set: in a weapon design script.

```lua weapon
Skin.Pattern = Pattern.Stripes
local value = Skin.Pattern --> Pattern.Stripes
```

#### `WeaponSkin.PatternColor: Color  (0 to 1)`

The pattern's first colour.

Set: in a weapon design script.

```lua weapon
Skin.PatternColor = Color.Black
local value = Skin.PatternColor --> #000000
```

#### `WeaponSkin.PatternColorEnd: Color  (0 to 1)`

The pattern's second colour.

Set: in a weapon design script.

```lua weapon
Skin.PatternColorEnd = Color.Yellow
local value = Skin.PatternColorEnd --> #ffff00
```

#### `WeaponSkin.PatternScale: number  (0.25 to 4)`

How large the pattern is: 1 as made.

Set: in a weapon design script.

```lua weapon
Skin.PatternScale = 0.5
local value = Skin.PatternScale --> 0.5
```

#### `WeaponSkin.PatternTurn: number  (0 to 180)`

How far the pattern is turned, in degrees.

Set: in a weapon design script.

```lua weapon
Skin.PatternTurn = 45
local value = Skin.PatternTurn --> 45
```

#### `WeaponSkin.PatternScroll: number  (-4 to 4)`

How fast the pattern scrolls along the weapon, its size a second; negative the other way.

Set: in a weapon design script.

```lua weapon
Skin.PatternScroll = 0.5
local value = Skin.PatternScroll --> 0.5
```

#### `WeaponSkin.PatternSpin: number  (-2 to 2)`

How fast the pattern spins, turns a second; negative the other way.

Set: in a weapon design script.

```lua weapon
Skin.PatternSpin = 0.25
local value = Skin.PatternSpin --> 0.25
```

#### `WeaponSkin.PatternShape: Picture`

The shape Pattern.Shapes repeats: a standard shape, a picture or one the script made (a star unless set).

Set: in a weapon design script.

```lua weapon
Skin.PatternShape = Shapes.Star6
local value = Skin.PatternShape --> Shapes.Star6
```

#### `WeaponSkin.Animation: SkinAnimation`

How the weapon's colour moves (each a stock skin script).

Set: in a weapon design script.

```lua weapon
Skin.Animation = SkinAnimation.Rainbow
local value = Skin.Animation --> SkinAnimation.Rainbow
```

#### `WeaponSkin.Speed: number  (0 to 10)`

How fast its colour moves: 1 is the clan tags' own pace.

Set: in a weapon design script.

```lua weapon
Skin.Speed = 2
local value = Skin.Speed --> 2
```

#### `WeaponSkin.Bands: number  (2 to 32)`

How many bands its colour moves along the weapon in.

Set: in a weapon design script.

```lua weapon
Skin.Bands = 12
local value = Skin.Bands --> 12
```

#### `WeaponSkin.Palette: {Color}`

The colours its motion goes through; the game's five (or its own for a pulse, glow or sheen: white) when none are given.

Set: in a weapon design script.

```lua weapon
Skin.Palette = { Color.Red, Color.Blue }
local value = Skin.Palette --> { #ff0000, #0000ff }
```

#### `WeaponSticker.Picture: Picture`

What the sticker shows: a picture, a standard shape or a shape or sticker of the player's own.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Picture = Picture.Heart
local value = star.Picture --> Picture.Heart
```

#### `WeaponSticker.Color: Color  (0 to 1)`

Its colour (a sticker of the player's own keeps its own colours).

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Color = Color.Red
local value = star.Color --> #ff0000
```

#### `WeaponSticker.Animation: Animation`

How its colour moves.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Animation = Animation.Rainbow
local value = star.Animation --> Animation.Rainbow
```

#### `WeaponSticker.Palette: {Color}`

The colours its colour cycle goes through.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Palette = { Color.Cyan, Color.Magenta }
local value = star.Palette --> { #00ffff, #ff00ff }
```

#### `WeaponSticker.AnimationSpeed: number  (0 to 10)`

How fast its colour moves: 1 is the game's own pace.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.AnimationSpeed = 2
local value = star.AnimationSpeed --> 2
```

#### `WeaponSticker.Position: Vector3  (-64 to 64)`

Where its middle is: forwards along the barrel (X), to the left (Y) and up (Z) from the weapon's origin; it is pressed onto the first surface it meets there.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Position = Vector3( 8, 0, 7 )
local value = star.Position --> Vector3( 8, 0, 7 )
```

#### `WeaponSticker.Rotation: Vector3  (-180 to 180)`

Which way it is pressed on, as pitch, yaw and roll in degrees from pressing down from above (roll -90 or 90 presses it onto a side).

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Rotation = Vector3( 0, 0, -90 )
local value = star.Rotation --> Vector3( 0, 0, -90 )
```

#### `WeaponSticker.Size: number  (0.5 to 12)`

How wide it is, in units.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Size = 4
local value = star.Size --> 4
```

#### `WeaponSticker.Finish: StickerFinish`

What it is printed on.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Finish = StickerFinish.Holo
local value = star.Finish --> StickerFinish.Holo
```

#### `WeaponSticker.Scrape: number  (0 to 1)`

How worn it is, 0 new to 1 scraped away in patches (the same on every machine).

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Scrape = 0.3
local value = star.Scrape --> 0.3
```

#### `WeaponSticker.Border: bool`

Whether it has a thin white die-cut border.

Set: in a weapon design script.

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Border = true
local value = star.Border --> true
```

#### `WeaponProp.Model: string`

The prop's model, by path (one of the stock props).

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Model = "models/citizen_props/hotdog01.vmdl"
local value = can.Model --> "models/citizen_props/hotdog01.vmdl"
```

#### `WeaponProp.Part: string`

The part of the weapon it is on (its magazine, slide...), by name: it follows that part through every animation; the whole weapon for "".

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Part = "magazine"
local value = can.Part --> "magazine"
```

#### `WeaponProp.Replaces: bool`

Whether it takes its part's place: the part is hidden, and the prop may be as large as the part.

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Replaces = true
local value = can.Replaces --> true
```

#### `WeaponProp.Position: Vector3  (-64 to 64)`

Where its middle sits: forwards, left and up from its part's middle (or the weapon's origin); kept within the weapon's bounds grown by 8 units.

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Position = Vector3( 2, 0, 10 )
local value = can.Position --> Vector3( 2, 0, 10 )
```

#### `WeaponProp.Rotation: Vector3  (-180 to 180)`

How it is turned: pitch, yaw and roll in degrees.

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Rotation = Vector3( 0, 90, 0 )
local value = can.Rotation --> Vector3( 0, 90, 0 )
```

#### `WeaponProp.Scale: number  (0.05 to 4)`

Its size: 1 as made; its longest side is held to 16 units (or its part's size).

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Scale = 0.8
local value = can.Scale --> 0.8
```

#### `WeaponProp.Nudge: Vector3  (-4 to 4)`

How far it is moved on the first-person weapon from where it is worked out to be: forwards, left and up, at most 4 units.

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Nudge = Vector3( 0, 0, 1 )
local value = can.Nudge --> Vector3( 0, 0, 1 )
```

#### `WeaponProp.NudgeRotation: Vector3  (-45 to 45)`

How far it is turned on the first-person weapon: pitch, yaw and roll, at most 45 degrees each.

Set: in a weapon design script.

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.NudgeRotation = Vector3( 0, 15, 0 )
local value = can.NudgeRotation --> Vector3( 0, 15, 0 )
```

#### `Effect.When: When`

When every layer of a weapon's effect shows: always, while something is so, or on a weapon event (When.OnShoot, OnReload, OnEquip, OnHolster, OnHit).

Set: in a weapon design script.

```lua weapon
local tracer = Weapon.AddEffect( Effects.Preset( "Bullet tracer" ) )
tracer.When = When.OnReload
local value = tracer.When --> When.OnReload
```

#### `Weapon: Weapon`

The weapon a design script is for: its finish and tint, and what is put on it (stickers, props, hidden parts, effects).

```lua weapon
Weapon.Finish = Finish.Chrome
local made = Weapon.Finish --> Finish.Chrome
```

#### `Skin: WeaponSkin`

What is painted over the weapon: a pattern from every side, and a colour moving over it.

```lua weapon
Skin.Pattern = Pattern.Camo
local laid = Skin.Pattern --> Pattern.Camo
```

#### `Weapon.AddSticker( picture: Picture ): WeaponSticker`

Sticks a picture onto the weapon, pressed onto its surface (at most 8): a picture, a standard shape, or a shape or sticker of the player's own.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `picture` | Picture | any | What it shows |

```lua weapon
local star = Weapon.AddSticker( Shapes.Star5 )
star.Position = Vector3( 8, 0, 7 )
local shown = star.Picture --> Shapes.Star5
```

#### `Weapon.AddProp( model: string ): WeaponProp`

Puts a stock prop on the weapon (at most 8), by its model's path: its longest side is held to 16 units, and it stays within the weapon's bounds grown by 8.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `model` | string | any | The model's path |

```lua weapon
local can = Weapon.AddProp( "models/sbox_props/cola_can/cola_can.vmdl" )
can.Scale = 0.5
local size = can.Scale --> 0.5
```

#### `Weapon.HidePart( part: string )`

Hides a part of the weapon by its name (a rail, the sights: what its model lets show or hide; at most 16); a part it doesn't have is passed over.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `part` | string | any | The part's name |

```lua weapon
Weapon.HidePart( "rail" )
```

#### `Weapon.AddEffect( effect: Effect ): Effect`

Puts an effect on the weapon, coming from it (at most 4, 8 layers between them): change its sliders and When on what it gives back.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `effect` | Effect | any | From Effects.Preset or Effects.Mine |

```lua weapon
local flash = Weapon.AddEffect( Effects.Preset( "Muzzle flash" ) )
flash.Hue = 0.5
local hue = flash.Hue --> 0.5
```

### Inputs

#### `time: number`

Milliseconds since the effect started (whole steps of the game's clock).

```lua sticker
local step = Math.Floor( time / 100 ) --> 15
```

#### `t: number`

Seconds since the effect started.

```lua sticker
local seconds = t --> 1.5
```

#### `speed: number`

How fast the body moves, in units a second.

```lua sticker
local running = speed > 100 --> true
```

#### `velocity: Vector3`

Which way and how fast the body moves (X forwards, Y left, Z up).

```lua sticker
local sideways = velocity.Y --> 120
```

#### `grounded: bool`

Whether the body stands on the ground.

```lua sticker
local onGround = grounded --> true
```

#### `crouching: bool`

Whether the body crouches.

```lua sticker
local low = crouching --> false
```

#### `moving: bool`

Whether the body moves.

```lua sticker
local going = moving --> true
```

#### `inAir: bool`

Whether the body is in the air.

```lua sticker
local flying = inAir --> false
```

#### `holdingWeapon: bool`

Whether the body holds a weapon.

```lua sticker
local armed = holdingWeapon --> false
```

#### `anchorSpeed: number`

How fast the effect's own place moves (a swung weapon's tip, a running foot).

```lua sticker
local swing = anchorSpeed / 100 --> 0.8
```

#### `anchorPosition: Vector3`

Where the effect's own place is in the world (its joint, item or weapon, moved by its Offset): to remember a path, or emit above it.

```lua effect
local glow = Sprite()
local start = Vector3.Zero
function OnSpawn()
	start = anchorPosition
end
function OnJump()
	glow.EmitAt( anchorPosition + Vector3( 0, 0, 60 ), 10 )
end
```

### Sticker

#### `i: number`

Which element this run colours, from 0: a particle, a shape point, an emblem layer, a band of a weapon's skin.

```lua sticker
local second = i == 1 --> false
```

#### `count: number`

How many elements there are (a weapon skin's bands).

```lua sticker
local last = i == count - 1 --> false
```

#### `life: number`

How far through its life the element is, 0 to 1 (a particle's; 0 for a shape's points).

```lua sticker
local young = life < 0.5 --> true
```

#### `origin: Vector3`

Where the element is on its shape, before its offset: a shape point's or emblem layer's x and y on the square (-1 to 1), a particle's place on its layer's shape.

```lua sticker
local right = origin.X > 0 --> true
local across = origin.X --> 0.5
```

#### `palette: {Color}`

The sticker's or weapon skin's palette (the game's five colours unless it has its own).

```lua sticker
local first = palette[ 0 ] --> #ff3333
local n = palette.Count --> 5
```

#### `highlight: Color`

The Cyln scanner's highlight: the palette's first colour when it has two or more, else red.

```lua sticker
local eye = highlight --> #ff3333
```

#### `rest: Color`

The Cyln scanner's other colour: the palette's second when it has two or more, else white.

```lua sticker
local other = rest --> #ffffff
```

#### `color: Color  (0 to 1)`

The element's colour (write it to change it); on a weapon's skin, the colour painted over the weapon, keeping its light and dark.

```lua sticker
color = Color.Red
local now = color --> #ff0000
```

#### `alpha: number  (0 to 1)`

How solid the element is, 0 to 1; on a weapon's skin, how much its colour covers the weapon (all of it unless set, once color is).

```lua sticker
alpha = 0.5
local now = alpha --> 0.5
```

#### `size: number  (0 to 48)`

How big the element is, times its own size.

```lua sticker
size = 1.5
local now = size --> 1.5
```

#### `rotation: number  (-3600 to 3600)`

How far the element is turned, in degrees.

```lua sticker
rotation = 90
local now = rotation --> 90
```

#### `offset: Vector3  (-24 to 24)`

How far the element is moved from its place.

```lua sticker
offset = Vector3( 0, 0, 1 )
local now = offset --> Vector3( 0, 0, 1 )
```

### Skin

#### `along: number`

Where along the weapon this run colours, 0 at its back to 1 at its front: a skin that reads it is worked out at 64 places along the weapon and blended between them, one that doesn't once a band.

```lua skin
local front = along > 0.5 --> false
```

#### `glow: Color  (0 to 4)`

The light the weapon gives off there, as a colour (black for none; up to 4 times as bright as white).

```lua skin
glow = Color( 1, 0.5, 0 )
local now = glow --> #ff8000
```

#### `sheen: number  (0 to 1)`

How glossy a sheen over the weapon makes it there, 0 as it is to 1 polished.

```lua skin
sheen = 0.5
local now = sheen --> 0.5
```

#### `shade: Color  (0 to 4)`

What the weapon's own colour is multiplied by there (white leaves it; up to 4 times brighter).

```lua skin
shade = Color( 1, 0.5, 0 )
local now = shade --> #ff8000
```

#### `scroll: number  (-1000000 to 1000000)`

How far the weapon's pattern is moved along it, in repeats (a gradient's a quarter as far).

```lua skin
scroll = t * 0.5
local now = scroll --> 0.75
```

#### `spin: number  (-1000000 to 1000000)`

How far the weapon's pattern is turned on top of its own angle, in turns.

```lua skin
spin = t * 0.25
local now = spin --> 0.375
```

### Events

#### `function OnSpawn()`

When the effect starts (put on, or shown again).

Every frame.

```lua effect
local burst = Sprite()
function OnSpawn()
	burst.Emit( 30 )
end
```

#### `function OnUpdate( dt: number )`

Every frame, with how long the frame took: change layers as things change.

Every frame.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `dt` | number | any | Seconds since the last frame |

```lua effect
local glow = Sprite()
function OnUpdate( dt )
	glow.Size = 2 + Math.Sin( t * 4 )
end
```

#### `function OnJump()`

When the player jumps.

At most 10 a second for each player.

```lua effect
local puff = Sprite()
function OnJump()
	puff.Emit( 10 )
end
```

#### `function OnLand( fallSpeed: number )`

When the player lands, with how fast they were falling.

At most 10 a second for each player.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `fallSpeed` | number | any | How fast they fell, in units a second |

```lua effect
local dust = Sprite()
function OnLand( fallSpeed )
	dust.Emit( Math.Min( fallSpeed / 20, 40 ) )
end
```

#### `function OnHurt( damage: number )`

When the player is hurt, with how much.

At most 10 a second for each player.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `damage` | number | any | How much health it took |

```lua effect
local blood = Sprite()
function OnHurt( damage )
	blood.Emit( damage )
end
```

#### `function OnDeath()`

When the player dies.

At most 10 a second for each player.

```lua effect
local soul = Sprite()
function OnDeath()
	soul.Emit( 40 )
end
```

#### `function OnRespawn()`

When the player comes back after dying.

At most 10 a second for each player.

```lua effect
local halo = Sprite()
function OnRespawn()
	halo.Emit( 20 )
end
```

#### `function OnFootstep( foot: Joint )`

When a foot touches down, with which one.

At most 10 a second for each player.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `foot` | Joint | any | Joint.LeftFoot or Joint.RightFoot |

```lua effect
local step = Sprite()
function OnFootstep( foot )
	if foot == Joint.LeftFoot then
		step.Emit( 3 )
	end
end
```

#### `function OnStartMoving()`

When the player starts moving.

At most 10 a second for each player.

```lua effect
local kick = Sprite()
function OnStartMoving()
	kick.Emit( 6 )
end
```

#### `function OnStopMoving()`

When the player stops moving.

At most 10 a second for each player.

```lua effect
local settle = Sprite()
function OnStopMoving()
	settle.Emit( 6 )
end
```

#### `function OnCrouch()`

When the player crouches.

At most 10 a second for each player.

```lua effect
local glow = Sprite()
function OnCrouch()
	glow.Off = true
end
```

#### `function OnUncrouch()`

When the player stands up from crouching.

At most 10 a second for each player.

```lua effect
local glow = Sprite()
function OnUncrouch()
	glow.Off = false
end
```

#### `function OnImpact( speed: number )`

When the body hits something hard, with how fast.

At most 10 a second for each player.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `speed` | number | any | How fast it hit, in units a second |

```lua effect
local crack = Sprite()
function OnImpact( speed )
	crack.Emit( speed / 50 )
end
```

#### `function OnShoot( shot: Shot )`

When the player shoots or swings, with the shot every client works out for itself.

At most 20 a second for each player.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `shot` | Shot | any | Origin, Direction, Hit, HitPosition, HitNormal, Distance |

```lua effect
local tracer = Sprite()
function OnShoot( shot )
	tracer.EmitAt( shot.HitPosition, 6 )
end
```

#### `function OnReload()`

When the player reloads.

At most 10 a second for each player.

```lua effect
local shells = Sprite()
function OnReload()
	shells.Emit( 5 )
end
```

#### `function OnEquip()`

When the player takes out a weapon.

At most 10 a second for each player.

```lua effect
local shine = Sprite()
function OnEquip()
	shine.Emit( 8 )
end
```

#### `function OnHolster()`

When the player puts a weapon away.

At most 10 a second for each player.

```lua effect
local shine = Sprite()
function OnHolster()
	shine.Emit( 8 )
end
```

#### `function OnHit()`

When the player's weapon hits something.

At most 10 a second for each player.

```lua effect
local spark = Sprite()
function OnHit()
	spark.Emit( 10 )
end
```

#### `function OnKill()`

When someone the player shot or hit dies a moment after (every client works it out from the shots and deaths it sees).

At most 10 a second for each player.

```lua effect
local kills = 0
local glow = Sprite()
function OnKill()
	kills = kills + 1
	glow.Emit( 10 )
end
```

#### `function OnKey( key: Key )`

When the effect's key is pressed (one key start per player every 2 minutes, for everyone).

At most 10 a second for each player.

| Parameter | Type | Range | What it is |
|---|---|---|---|
| `key` | Key | any | The key, by name |

```lua effect
local flash = Sprite()
function OnKey( key )
	if key == "g" then
		flash.Emit( 50 )
	end
end
```

### Shot

#### `Shot.Origin: Vector3  (read only)`

Where the shot left the weapon (its muzzle), in the world.

Set: read only.

```lua effect
local sparks = Sprite()
function OnShoot( shot )
	sparks.EmitAt( shot.Origin, 4 )
end
```

#### `Shot.Direction: Vector3  (read only)`

Which way it went: the aim (spread isn't known on other machines).

Set: read only.

```lua effect
local sparks = Sprite()
function OnShoot( shot )
	sparks.EmitAt( shot.Origin + shot.Direction * 8, 4 )
end
```

#### `Shot.Hit: bool  (read only)`

Whether it hit something.

Set: read only.

```lua effect
local sparks = Sprite()
function OnShoot( shot )
	if shot.Hit then
		sparks.Emit( 4 )
	end
end
```

#### `Shot.HitPosition: Vector3  (read only)`

Where it hit (where it ran out, when it hit nothing).

Set: read only.

```lua effect
local sparks = Sprite()
function OnShoot( shot )
	sparks.EmitAt( shot.HitPosition, 12 )
end
```

#### `Shot.HitNormal: Vector3  (read only)`

The way the surface it hit faces.

Set: read only.

```lua effect
local sparks = Sprite()
function OnShoot( shot )
	sparks.EmitAt( shot.HitPosition + shot.HitNormal * 2, 8 )
end
```

#### `Shot.Distance: number  (read only)`

How far it went.

Set: read only.

```lua effect
local sparks = Sprite()
function OnShoot( shot )
	sparks.Emit( Math.Min( shot.Distance / 100, 20 ) )
end
```

### Choices

#### `Kind`

What a layer is: sprites, a light, or a trail it leaves.

`Kind.Sprite`, `Kind.Light`, `Kind.Trail`, `Kind.Beam`, `Kind.Ghost`

```lua sticker
local picked = Kind.Light --> Kind.Light
```

#### `Joint`

A joint of the body an effect or layer comes from (Joint.Ground: the ground under it, turned the way it faces).

`Joint.Head`, `Joint.Eyes`, `Joint.LeftEye`, `Joint.RightEye`, `Joint.Crown`, `Joint.Neck`, `Joint.Chest`, `Joint.Hips`, `Joint.LeftHand`, `Joint.RightHand`, `Joint.LeftFoot`, `Joint.RightFoot`, `Joint.Ground`

```lua sticker
local picked = Joint.Eyes --> Joint.Eyes
```

#### `ComesFrom`

Where a layer's sprites come from around its place ("Comes from").

`ComesFrom.Point`, `ComesFrom.Sphere`, `ComesFrom.Ring`, `ComesFrom.AroundBody`, `ComesFrom.Box`, `ComesFrom.Cone`, `ComesFrom.Line`, `ComesFrom.Shape`, `ComesFrom.Shot`

```lua sticker
local picked = ComesFrom.Sphere --> ComesFrom.Sphere
```

#### `Facing`

The way a layer faces from its joint: where a cone opens, a line runs, a shape looks.

`Facing.Forward`, `Facing.Up`, `Facing.Down`, `Facing.Back`, `Facing.Left`, `Facing.Right`, `Facing.Flat`, `Facing.Camera`

```lua sticker
local picked = Facing.Up --> Facing.Up
```

#### `ColorMode`

How a layer's sprites are coloured: fading, a World at War cycle, or the palette in turn.

`ColorMode.Fixed`, `ColorMode.Rain`, `ColorMode.Cycl`, `ColorMode.Cyln`, `ColorMode.Mix`

```lua sticker
local picked = ColorMode.Rain --> ColorMode.Rain
```

#### `Rhythm`

How a layer changes over time.

`Rhythm.Steady`, `Rhythm.Pulse`, `Rhythm.Flicker`, `Rhythm.Rainbow`, `Rhythm.Spin`, `Rhythm.Jiggle`

```lua sticker
local picked = Rhythm.Pulse --> Rhythm.Pulse
```

#### `When`

When a layer shows: always, while something is so, or for a moment when something happens.

`When.Always`, `When.Moving`, `When.Grounded`, `When.InAir`, `When.Crouching`, `When.HoldingWeapon`, `When.OnJump`, `When.OnLand`, `When.OnHurt`, `When.OnStartMoving`, `When.OnStopMoving`, `When.OnCrouch`, `When.OnUncrouch`, `When.OnShoot`, `When.OnImpact`, `When.OnReload`, `When.OnEquip`, `When.OnHolster`, `When.OnHit`, `When.OnDeath`, `When.OnRespawn`, `When.OnFootstep`, `When.OnKill`

```lua sticker
local picked = When.Moving --> When.Moving
```

#### `Anchor`

What an effect comes from: a joint, an item worn, or the weapon held.

`Anchor.Joint`, `Anchor.Item`, `Anchor.HeldWeapon`

```lua sticker
local picked = Anchor.Item --> Anchor.Item
```

#### `Animation`

How a sticker's (or emblem layer's) colour moves.

`Animation.None`, `Animation.Rainbow`, `Animation.Rain`, `Animation.Cycl`, `Animation.Cyln`, `Animation.Pulse`

```lua sticker
local picked = Animation.Rainbow --> Animation.Rainbow
```

#### `Align`

Which way a layer's sprites face: the camera, a swing's plane, flat on the ground, the way the layer faces, or out from its place.

`Align.Camera`, `Align.Swing`, `Align.Ground`, `Align.Facing`, `Align.Outward`

```lua sticker
local picked = Align.Swing --> Align.Swing
```

#### `Finish`

What a weapon is made of: its own materials (None), or one of the stock finishes.

`Finish.None`, `Finish.Gold`, `Finish.Silver`, `Finish.Chrome`, `Finish.Copper`, `Finish.Bronze`, `Finish.Gloss`, `Finish.Clay`, `Finish.Matteblack`

```lua sticker
local picked = Finish.Gold --> Finish.Gold
```

#### `Pattern`

What is laid over a weapon from every side: stripes, camo, checker, a gradient, carbon fibre or a repeated shape.

`Pattern.None`, `Pattern.Stripes`, `Pattern.Camo`, `Pattern.Checker`, `Pattern.Gradient`, `Pattern.Carbon`, `Pattern.Shapes`

```lua sticker
local picked = Pattern.Stripes --> Pattern.Stripes
```

#### `SkinAnimation`

How a weapon's colour moves: a rainbow flowing along it, World at War's colourings in bands, a pulse, a glow, a sheen or its palette going round.

`SkinAnimation.None`, `SkinAnimation.Rainbow`, `SkinAnimation.Rain`, `SkinAnimation.Cycl`, `SkinAnimation.Cyln`, `SkinAnimation.Pulse`, `SkinAnimation.Glow`, `SkinAnimation.Shimmer`, `SkinAnimation.Palette`

```lua sticker
local picked = SkinAnimation.Rainbow --> SkinAnimation.Rainbow
```

#### `StickerFinish`

What a sticker is printed on: paper, holo, foil, glitter, gold or lenticular.

`StickerFinish.Paper`, `StickerFinish.Holo`, `StickerFinish.Foil`, `StickerFinish.Glitter`, `StickerFinish.Gold`, `StickerFinish.Lenticular`

```lua sticker
local picked = StickerFinish.Holo --> StickerFinish.Holo
```

### Keys

The keys an effect can be shown with, written as text (any other text is a compile error):

`"a"`, `"b"`, `"c"`, `"d"`, `"e"`, `"f"`, `"g"`, `"h"`, `"i"`, `"j"`, `"k"`, `"l"`, `"m"`, `"n"`, `"o"`, `"p"`, `"q"`, `"r"`, `"s"`, `"t"`, `"u"`, `"v"`, `"w"`, `"x"`, `"y"`, `"z"`, `"0"`, `"1"`, `"2"`, `"3"`, `"4"`, `"5"`, `"6"`, `"7"`, `"8"`, `"9"`, `"f1"`, `"f2"`, `"f3"`, `"f4"`, `"f5"`, `"f6"`, `"f7"`, `"f8"`, `"f9"`, `"f10"`, `"f11"`, `"f12"`, `"mouse4"`, `"mouse5"`

```lua effect
local glow = Sprite()
Effect.Key = "g"
```
<!-- reference:end -->
