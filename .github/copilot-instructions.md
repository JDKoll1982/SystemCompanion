# Copilot instructions — System Companion

The project constitution at `.specify/memory/constitution.md` is the source of
truth. This file only points at the rules an editor is most likely to break.
When the two disagree, the constitution wins.

## Layout: Principle VI (proportional layout at every display scale)

WinUI 3 lays out in effective pixels, so Windows display scaling already enlarges
anything allowed to grow. Do not fight it.

- Do not put a fixed `Width` or `Height` on anything that carries text, a list, or
  a card. Use `*` grid tracks, `MinWidth`/`MaxWidth`/`MinHeight`/`MaxHeight`, or
  content-driven `Auto` tracks.
- `*` is a grid track value only. `Width="*"` on an element is not valid XAML.
- Collections reflow or scroll. A horizontal `StackPanel` that can exceed the
  window goes inside a horizontal `ScrollViewer` or is re-laid out at a width.
- A layout that changes structure at a width reflows at a measured width. Prefer
  `VisualStateManager` with `AdaptiveTrigger.MinWindowWidth`; where the trigger
  does not fire in this host, drive the same states from `SizeChanged`.
  (`HistoryPage`, `BiosNowPage` and `ImprovementsPage` do this today.)
- Clamp the startup window to the display work area. `MainWindow.SizeToWorkArea`
  does it.
- `Image` and `PersonPicture` set `Stretch`. `ViewBox` is for vector art only,
  never for text or controls.
- Fixed sizes are allowed only for decorative marks smaller than a control, and
  for dimensions a control owns (`NavigationView.OpenPaneLength`, the title
  strip's minimum height).

## Other non-negotiables

- Read-only: never write to the OS, the firmware, a device, or `Data/bios/`.
- Plain language everywhere; an unreadable value is exactly `Unknown`.
- No new UI, MVVM, or DI package. Change notification stays on `ObservableBase`.
- Paths resolve through `Services/AppData.cs`.
- A build reports zero warnings from project code.

Verify a UI change at 100%, 150% and 200% display scaling and in a narrow window:
no clipped text, no page forced wider than the window.
