using HarmonyLib;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Meitou.Content;
using Meitou.Data;
using Meitou.Data.Fcs;
using Meitou.Data.Gameplay;
using Meitou.Data.Gameplay.Bodies;
using Meitou.Data.Gameplay.Construction;
using Meitou.Modding;
using Meitou.Simulation;
using Meitou.Simulation.Bodies;
using Meitou.Simulation.Items;
using Meitou.Simulation.Production;
using Machine = Meitou.Simulation.Production.Machine;

var database = new GameDatabase();
var records = new FcsFile();
records.Records.Add(new FcsRecord { StringId = "test-armour", Name = "Test armour", RecordType = FcsRecordType.Armour, Flags = 0x10 });
database.Apply(records, "test.mod");
World? currentWorld = null;
bool savedSettingsCheck = args is ["--saved-settings"];
bool savedMultiplierCheck = args is ["--saved-multiplier"];
IReadOnlyDictionary<string, string>? saved = savedSettingsCheck || savedMultiplierCheck
    ? new Dictionary<string, string>
    {
        ["OverrideEnabled"] = "true", ["ChancePercent"] = "37",
        ["Mode"] = savedMultiplierCheck ? "Multiplier" : "Fixed", ["Multiplier"] = "2",
    }
    : null;
var folder = Environment.GetEnvironmentVariable("CRAFTCRIT_MOD_FOLDER") ?? Path.Combine(AppContext.BaseDirectory, "Mod");
var info = new ModInfo("CraftCrit-Meitou", Path.Combine(folder, "CraftCrit-Meitou.mod"), ModSource.Local);
// Catch an unversioned source build (1.0.0.0) being shipped to a lower-versioned release (for example 0.22.1.0).
using (var stream = File.OpenRead(Path.Combine(folder, "CraftCrit-Meitou.dll")))
using (var pe = new PEReader(stream))
{
    var metadata = pe.GetMetadataReader();
    foreach (var handle in metadata.AssemblyReferences)
    {
        var reference = metadata.GetAssemblyReference(handle);
        string name = metadata.GetString(reference.Name);
        if (!name.StartsWith("Meitou", StringComparison.Ordinal)) continue;
        var supplied = AssemblyName.GetAssemblyName(Path.Combine(AppContext.BaseDirectory, name + ".dll"));
        Check(supplied.Version >= reference.Version,
            $"{name}: mod requires {reference.Version}, client supplies {supplied.Version}; rebuild against the installed client");
    }
}
var host = ModHost.Load(new ModHostOptions([info], database, null!, () => currentWorld, SavedSettings: _ => saved));
Check(host.Mods.Count == 1 && host.Notices.Count == 0, "mod loads and installs its Harmony patches");
var group = host.Settings.Single();
var enabled = group.Settings.Single(setting => setting.Key == "OverrideEnabled");
var chance = group.Settings.Single(setting => setting.Key == "ChancePercent");
var mode = group.Settings.Single(setting => setting.Key == "Mode");
var multiplier = group.Settings.Single(setting => setting.Key == "Multiplier");
Check(Equals(enabled.Get(), true) && Equals(chance.Get(), savedSettingsCheck || savedMultiplierCheck ? 37 : 100),
    "defaults or persisted settings load correctly");
Check(mode.Get()!.ToString() == (savedMultiplierCheck ? "Multiplier" : "Fixed"), "chance mode is restored or defaults to fixed");
Check(Equals(multiplier.Get(), 2f), "multiplier defaults to 2x");
Check(chance.Min == 0 && chance.Max == 100 && chance.Step == 1, "native slider spans 0–100 in whole percentages");
var factory = host.Simulation!.Systems.Single();
Check(factory.Anchor == SystemAnchor.Before(StandardParts.Production), "settings system runs before production");
var constants = GameConstants.Default with { ProductionSpeed = 2 };
var production = new ProductionSystem(database, constants, BodyOptions.Default, new ItemFactory(database, constants), null);
var system = factory.Create(new SystemBuildContext(null!, [production]))!;
using var world = new World(new WorldSettings { Seed = 918273, PublishSnapshots = false },
    new OpenGroundWalkability((_, _) => 0), [system]);
currentWorld = world;
var craftStep = AccessTools.DeclaredMethod(typeof(ProductionSystem), "CraftStep");

var option = new CraftOption(CraftKind.Armour, "test-armour", null, 0, 1000);
var machine = new Machine
{
    Site = "test-bench",
    // The real leather bench has tech level 25; older clients incorrectly capped all completed clothing at Shoddy.
    Data = new MachineData { Id = "test-bench", Name = "Test bench", Stat = 8, Rate = 1, Craft = new CraftBench { Options = [option], TechLevel = 25 } },
    Craft = new CraftState(),
};

CraftJob Roll(float skill = 80)
{
    var job = new CraftJob(new CraftRecipe(option, "Test armour", 1, [], 80));
    machine.Craft.Queue.Clear();
    machine.Craft.Queue.Add(job);
    craftStep.Invoke(production, [world, machine, skill, 1f, 0f]);
    return job;
}

void ApplySettings()
{
    var due = world.Commands.TakeDue(world.Tick);
    system.Inputs(world, due);
}

if (savedSettingsCheck || savedMultiplierCheck)
{
    for (int i = 0; i < 512; i++)
    {
        float roll = Rng.Float(Rng.Hash(world.Seed, Rng.StableHash(machine.Site), RngPurpose.Craft, (ulong)(uint)machine.Craft.Rolls));
        float expected = savedMultiplierCheck ? CraftBench.CriticalChance(99, machine.Data.Stat, option.Level) * 2 : 0.37f;
        Check(Roll(99).Critical == (roll < expected ? 1 : 0), "new worlds use the persisted mode and value from the first roll");
    }

    Console.WriteLine($"PASS: persisted {(savedMultiplierCheck ? "2x multiplier" : "37% fixed chance")} restored and used from the first roll.");
    return;
}

for (int i = 0; i < 128; i++) Check(Roll().Critical == 1, "100% always crits even at zero vanilla chance");
chance.Set(0);
Check(Roll().Critical == 1, "UI changes wait for the serial input phase");
var version = world.Machines.Version;
ApplySettings();
Check(world.Machines.Version > version, "settings invalidate cached crafting predictions");
for (int i = 0; i < 128; i++) Check(Roll(130).Critical == 0, "0% never crits even at 100% vanilla chance");

chance.Set(37);
ApplySettings();
for (int i = 0; i < 512; i++)
{
    int counter = machine.Craft.Rolls;
    float roll = Rng.Float(Rng.Hash(world.Seed, Rng.StableHash(machine.Site), RngPurpose.Craft, (ulong)(uint)counter));
    Check(Roll().Critical == (roll < 0.37f ? 1 : 0), "37% uses the engine's unchanged seeded roll and counter");
    Check(machine.Craft.Rolls == counter + 1, "one deterministic roll per new craft");
}

var existing = Roll();
byte existingOutcome = existing.Critical;
int existingCounter = machine.Craft.Rolls;
chance.Set(100);
ApplySettings();
craftStep.Invoke(production, [world, machine, 80f, 1f, 0f]);
Check(existing.Critical == existingOutcome && machine.Craft.Rolls == existingCounter, "settings preserve previously rolled craft outcomes");

enabled.Set(false);
ApplySettings();
for (int i = 0; i < 64; i++)
{
    int counter = machine.Craft.Rolls;
    float vanilla = CraftBench.CriticalChance(99, machine.Data.Stat, option.Level);
    float roll = Rng.Float(Rng.Hash(world.Seed, Rng.StableHash(machine.Site), RngPurpose.Craft, (ulong)(uint)counter));
    Check(Roll(99).Critical == (roll < vanilla ? 1 : 0), "override off restores the actual vanilla calculation");
}

chance.Set(-50);
Check(Equals(chance.Get(), 0), "settings clamp values below zero");
chance.Set(150);
Check(Equals(chance.Get(), 100), "settings clamp values above 100");
chance.Set(53);
enabled.Set(true);
ApplySettings();

// Exercise the actual preview calculation with a synthetic smith and bench, including an item without a chance row.
var stats = new CharacterStats();
stats[(StatsEnumerated)8] = 99;
machine.Smith = world.Characters.Spawn(new CharacterHot(), new CharacterCold
{
    Name = "Test smith", Stats = stats, Medical = new MedicalState([]),
}, world.Tick);
machine.Craft.Queue.Add(new CraftJob(new CraftRecipe(new CraftOption(CraftKind.Item, "test-item", null, 0, 10), "Test item", 1, [], 0)));
AccessTools.DeclaredMethod(typeof(MachineRegistry), "Add").Invoke(world.Machines, [machine]);
for (int percent = 0; percent <= 100; percent++)
{
    chance.Set(percent);
    ApplySettings();
    var preview = production.SmithsView(world)[machine.Site];
    Check(preview.Entries[0].Critical == percent, "Craft tab displays every exact percentage, including float rounding cases");
    Check(preview.Entries[1].Critical == -1, "items without a critical chance row keep it hidden");
}

Check(chance.Visible && !multiplier.Visible, "fixed mode shows only its own slider");
multiplier.Set(9);
ApplySettings();
Check(Roll().Critical == 1, "inactive multiplier does not affect fixed mode");

mode.Set("Multiplier");
multiplier.Set(2);
ApplySettings();
Check(!chance.Visible && multiplier.Visible, "multiplier mode shows only its own slider");
Check(Roll(80).Critical == 0, "multiplying a zero base chance still gives zero");
for (int i = 0; i < 512; i++)
{
    float expected = CraftBench.CriticalChance(99, machine.Data.Stat, option.Level) * 2;
    float roll = Rng.Float(Rng.Hash(world.Seed, Rng.StableHash(machine.Site), RngPurpose.Craft, (ulong)(uint)machine.Craft.Rolls));
    Check(Roll(99).Critical == (roll < expected ? 1 : 0), "2x multiplier uses the raw vanilla probability and seeded roll");
}

// 99.9 skill has a fractional base percentage: multiplying the already truncated display would lose precision.
stats[(StatsEnumerated)8] = 99.9f;
world.Machines.Touch();
int expectedPreview = (int)(CraftBench.CriticalChance(99.9f, machine.Data.Stat, option.Level) * 2 * 100);
Check(production.SmithsView(world)[machine.Site].Entries[0].Critical == expectedPreview,
    "multiplier preview uses the unrounded vanilla chance");
chance.Set(0);
ApplySettings();
Check(production.SmithsView(world)[machine.Site].Entries[0].Critical == expectedPreview,
    "inactive fixed percentage does not affect multiplier mode");

foreach (float factor in new[] { 0f, 0.5f, 1f, 10f })
{
    multiplier.Set(factor);
    ApplySettings();
    for (int i = 0; i < 64; i++)
    {
        float expected = Math.Clamp(CraftBench.CriticalChance(99, machine.Data.Stat, option.Level) * factor, 0, 1);
        float roll = Rng.Float(Rng.Hash(world.Seed, Rng.StableHash(machine.Site), RngPurpose.Craft, (ulong)(uint)machine.Craft.Rolls));
        Check(Roll(99).Critical == (roll < expected ? 1 : 0), "0x, fractional, 1x, and capped multipliers follow the normal probability");
    }
}

enabled.Set(false);
ApplySettings();
Check(!chance.Visible && !multiplier.Visible, "turning the override off hides both value sliders");
Check(Roll(80).Critical == 0, "disabled multiplier restores vanilla");
enabled.Set(true);
mode.Set("Fixed");
ApplySettings();

var otherProduction = new ProductionSystem(database, constants, BodyOptions.Default, new ItemFactory(database, constants), null);
chance.Set(0);
ApplySettings();
var otherSystem = factory.Create(new SystemBuildContext(null!, [otherProduction]))!;
chance.Set(100);
ApplySettings();
var otherMachine = new Machine { Site = "other-bench", Data = machine.Data, Craft = new CraftState() };
otherMachine.Craft.Queue.Add(new CraftJob(new CraftRecipe(option, "Test armour", 1, [], 80)));
craftStep.Invoke(otherProduction, [world, otherMachine, 130f, 1f, 0f]);
Check(otherMachine.Craft.Queue[0].Critical == 0 && Roll().Critical == 1, "production systems keep independent per-world settings");
Check(system is IStateHashed && otherSystem is IStateHashed, "active settings participate in the state hash");
var firstHash = new StateHasher();
var secondHash = new StateHasher();
((IStateHashed)system).Hash(ref firstHash);
((IStateHashed)otherSystem).Hash(ref secondHash);
Check(firstHash.Value != secondHash.Value, "different active percentages produce different hashes");

// Verify finished inventory items through the operator work path, not just the roll flag.
var workStep = AccessTools.DeclaredMethod(typeof(ProductionSystem), "WorkStep");
ItemInstance CompleteArmour(float skill)
{
    stats[(StatsEnumerated)8] = skill;
    machine.Craft.Done.Clear();
    machine.Craft.Queue.Clear();
    machine.Craft.Queue.Add(new CraftJob(new CraftRecipe(option, "Test armour", 1, [], 80)));
    var smith = world.Characters.Cold(machine.Smith.Slot)!;
    workStep.Invoke(production, [world, machine.Smith.Slot, smith, machine, 1000f, 0]);
    Check(machine.Craft.Done.Count == 1, "operator work completes an actual armour item");
    return machine.Craft.Done.Single();
}

mode.Set("Fixed");
chance.Set(100);
ApplySettings();
var guaranteed = CompleteArmour(100);
Console.WriteLine($"Finished armour at skill 100, fixed 100%: quality={guaranteed.Quality}, level={guaranteed.Level}");
Check(guaranteed.Level == 95, "100% crit produces Masterwork at skill 100");
chance.Set(0);
ApplySettings();
Check(CompleteArmour(100).Level == 80, "0% crit produces Specialist at skill 100");
mode.Set("Multiplier");
multiplier.Set(10);
ApplySettings();
Check(CompleteArmour(100).Level == 95, "capped multiplier produces Masterwork at skill 100");
enabled.Set(false);
ApplySettings();
for (int i = 0; i < 32; i++) Check(CompleteArmour(100).Level is 80 or 95, "override off retains high-skill armour quality");

Console.WriteLine("PASS: real loader and crafting/preview patches; exclusive fixed/multiplier modes; exact previews; queued changes; existing rolls; world isolation and hashing; finished grades at a tech-level-25 bench.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + message);
}
