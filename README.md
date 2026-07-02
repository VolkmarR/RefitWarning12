# Refit 13 + `InternalsVisibleTo` → CS0436 warning

This repository is a minimal reproduction of a build warning (`CS0436`) that
appears in a test project when a web/library project uses **Refit 13** and
exposes its internals via `InternalsVisibleTo`.

## The warning

```
warning CS0436: The type 'PrimitivesR3BridgeGeneratedAttribute' in
'.../TestProject1/obj/.../ReactiveUI.Primitives.R3Bridge.Generator/.../PrimitivesR3BridgeGeneratedAttribute.g.cs'
conflicts with the imported type 'PrimitivesR3BridgeGeneratedAttribute' in
'WebApplication1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'.
Using the type defined in
'.../PrimitivesR3BridgeGeneratedAttribute.g.cs'.
[.../TestProject1/TestProject1.csproj]
```

The warning is reported against **`TestProject1`**, not against the project that
actually references Refit.

## Why it happens

1. `WebApplication1` references `Refit.HttpClientFactory` **13.0.0**. Refit 13
   ships a source generator (via `ReactiveUI.Primitives.R3Bridge.Generator`)
   that emits an **`internal`** marker type,
   `PrimitivesR3BridgeGeneratedAttribute`, into the compiling assembly.

2. `WebApplication1` opts into `InternalsVisibleTo("TestProject1")`, so the
   generated **internal** `PrimitivesR3BridgeGeneratedAttribute` in
   `WebApplication1` becomes visible to `TestProject1`.

3. `TestProject1` has a `ProjectReference` to `WebApplication1`. That reference
   pulls the Refit source generator into `TestProject1`'s compilation as well,
   so the **same** `PrimitivesR3BridgeGeneratedAttribute` is *also generated
   locally* inside `TestProject1`.

4. The compiler now sees two types with the same name: the one generated
   locally in `TestProject1` **and** the one imported from `WebApplication1`
   (now visible thanks to `InternalsVisibleTo`). That collision is exactly what
   `CS0436` reports. It picks the locally generated one and warns.

Remove the `InternalsVisibleTo` and the imported type is no longer visible, so
there is nothing to collide with and the warning disappears.

## Project layout

| Project           | Role                                                                 |
| ----------------- | -------------------------------------------------------------------- |
| `WebApplication1` | References Refit 13 and declares `InternalsVisibleTo("TestProject1")`. Contains an `internal` Refit interface. |
| `TestProject1`    | xUnit test project with a `ProjectReference` to `WebApplication1`.   |

The relevant part of `WebApplication1/WebApplication1.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="Refit.HttpClientFactory" Version="13.0.0" />
</ItemGroup>

<!-- by adding this, the testproject shows the "PrimitivesR3BridgeGeneratedAttribute.g.cs(2,61): Warning CS0436" -->
<ItemGroup>
    <InternalsVisibleTo Include="TestProject1" />
</ItemGroup>
```

## Reproduce

Requirements: .NET SDK 10 (the projects target `net10.0`).

```bash
# A clean/full rebuild is required — the generated file is cached, so an
# incremental build does not re-emit it and the warning stays hidden.
dotnet build --no-incremental
```

Look for `warning CS0436` referencing `PrimitivesR3BridgeGeneratedAttribute`
in the `TestProject1` build output.

## Confirm it is caused by `InternalsVisibleTo`

Comment out or remove the `InternalsVisibleTo` item group in
`WebApplication1/WebApplication1.csproj`, then rebuild:

```bash
dotnet build --no-incremental
```

The `CS0436` warning no longer appears.

## Notes

- The interface in `WebApplication1/IRefitInterface.cs` only needs to exist to
  make the project a realistic Refit consumer; the warning stems from the
  generator-emitted marker attribute, not from the interface itself.
- Because the generated file is cached under `obj/`, always use
  `--no-incremental` (or delete `bin`/`obj`) when reproducing.
