# Item Settings Panel Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix 5 design system violations in the Item Settings slide-in drawer so it is consistent with the Precision Dark design system.

**Architecture:** All changes are isolated to two files: `CardWindowStyles.xaml` (add one new style) and `CardWindow.xaml` (fix 4 property/layout issues). No ViewModel or C# changes needed. No unit tests apply to XAML styling — verification is visual (build + run).

**Tech Stack:** WPF XAML, C# (.NET 8), `dotnet build`, `pwsh scripts/run-debug.ps1`

---

### Task 1: Add `SettingsCheckBoxStyle` to CardWindowStyles.xaml

**Files:**
- Modify: `src/WinAppProfiles.UI/Resources/CardWindowStyles.xaml` — insert after line ~158 (end of `ToggleSwitchStyle`)

**Context:** The existing `ToggleSwitchStyle` is defined for card toggles. There is no styled `CheckBox` for form controls. The settings panel currently uses a raw `<CheckBox>` which renders as a Windows-native white square.

**Step 1: Find the insertion point**

Open `src/WinAppProfiles.UI/Resources/CardWindowStyles.xaml`. Find the closing `</Style>` that ends `ToggleSwitchStyle` (the toggle switch, ends around line 158). Insert the new style immediately after it.

**Step 2: Add the style**

Insert this block after `ToggleSwitchStyle`'s closing `</Style>`:

```xml
<!-- Settings Form Checkbox Style -->
<Style x:Key="SettingsCheckBoxStyle" TargetType="CheckBox">
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="CheckBox">
                <Border x:Name="Box"
                        Width="16" Height="16"
                        CornerRadius="3"
                        BorderThickness="1.5"
                        VerticalAlignment="Center">
                    <Border.BorderBrush>
                        <SolidColorBrush x:Name="BoxBorder" Color="#2A2A35"/>
                    </Border.BorderBrush>
                    <Border.Background>
                        <SolidColorBrush x:Name="BoxFill" Color="Transparent"/>
                    </Border.Background>
                    <Path x:Name="Tick"
                          Data="M 2 8 L 6 12 L 14 4"
                          Stroke="White"
                          StrokeThickness="1.5"
                          StrokeStartLineCap="Round"
                          StrokeEndLineCap="Round"
                          StrokeLineJoin="Round"
                          Visibility="Collapsed"
                          HorizontalAlignment="Center"
                          VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="BoxFill" Property="Color" Value="#7C6FCD"/>
                        <Setter TargetName="BoxBorder" Property="Color" Value="#7C6FCD"/>
                        <Setter TargetName="Tick" Property="Visibility" Value="Visible"/>
                    </Trigger>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="BoxBorder" Property="Color" Value="#9993B4"/>
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="Box" Property="Opacity" Value="0.4"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**Step 3: Build to verify no XAML errors**

```bash
dotnet build src/WinAppProfiles.UI/WinAppProfiles.UI.csproj -c Debug
```

Expected: `Build succeeded.` with 0 errors.

**Step 4: Commit**

```bash
git add src/WinAppProfiles.UI/Resources/CardWindowStyles.xaml
git commit -m "feat: add SettingsCheckBoxStyle for settings panel form controls"
```

---

### Task 2: Fix the drawer panel background and left border

**Files:**
- Modify: `src/WinAppProfiles.UI/Views/CardWindow.xaml` — the drawer `Border` around line 564

**Context:** The drawer's outer `Border` uses `BackgroundTertiary` (`#222228`) as its background and `BackgroundQuaternary` (`#16161A`) as its left border. `BackgroundTertiary` is the token for *input fields*, not panels. The border color is nearly invisible against that background.

**Step 1: Locate the drawer Border**

Find this block in `CardWindow.xaml` (around line 564):

```xml
<Border HorizontalAlignment="Right" Width="380"
        Background="{StaticResource BackgroundTertiary}"
        BorderBrush="{StaticResource BackgroundQuaternary}"
        BorderThickness="1,0,0,0">
```

**Step 2: Apply the fixes**

Replace with:

```xml
<Border HorizontalAlignment="Right" Width="380"
        Background="{StaticResource BackgroundSecondary}"
        BorderBrush="{StaticResource CardBorderBrush}"
        BorderThickness="1,0,0,0">
```

Changes:
- `BackgroundTertiary` → `BackgroundSecondary` (`#1A1A20`) — correct token for panel backgrounds
- `BackgroundQuaternary` → `CardBorderBrush` (`#2A2A35`) — visible 1px dividing line

**Step 3: Build to verify**

```bash
dotnet build src/WinAppProfiles.UI/WinAppProfiles.UI.csproj -c Debug
```

Expected: `Build succeeded.`

**Step 4: Commit**

```bash
git add src/WinAppProfiles.UI/Views/CardWindow.xaml
git commit -m "fix: correct drawer background token and left border visibility"
```

---

### Task 3: Apply SettingsCheckBoxStyle to both checkboxes

**Files:**
- Modify: `src/WinAppProfiles.UI/Views/CardWindow.xaml` — two `CheckBox` elements in the drawer

**Context:** Both `CheckBox` elements in the settings panel have no `Style` set, causing the native Windows rendering.

**Step 1: Find and fix the Battery Mode Only checkbox**

Find (around line 744):

```xml
<CheckBox DockPanel.Dock="Right"
          IsChecked="{Binding EditOnlyApplyOnBattery, Mode=TwoWay}"
          VerticalAlignment="Top"/>
```

Replace with:

```xml
<CheckBox DockPanel.Dock="Right"
          IsChecked="{Binding EditOnlyApplyOnBattery, Mode=TwoWay}"
          Style="{StaticResource SettingsCheckBoxStyle}"
          VerticalAlignment="Top"/>
```

**Step 2: Find and fix the Force Minimized checkbox**

Find (around line 758):

```xml
<CheckBox DockPanel.Dock="Right"
          IsChecked="{Binding EditForceMinimizedOnStart, Mode=TwoWay}"
          IsEnabled="{Binding IsEditDesiredRunning}"
          VerticalAlignment="Top"/>
```

Replace with:

```xml
<CheckBox DockPanel.Dock="Right"
          IsChecked="{Binding EditForceMinimizedOnStart, Mode=TwoWay}"
          IsEnabled="{Binding IsEditDesiredRunning}"
          Style="{StaticResource SettingsCheckBoxStyle}"
          VerticalAlignment="Top"/>
```

**Step 3: Build to verify**

```bash
dotnet build src/WinAppProfiles.UI/WinAppProfiles.UI.csproj -c Debug
```

Expected: `Build succeeded.`

**Step 4: Commit**

```bash
git add src/WinAppProfiles.UI/Views/CardWindow.xaml
git commit -m "fix: apply SettingsCheckBoxStyle to settings panel checkboxes"
```

---

### Task 4: Fix the SELECT ICON ComboBox

**Files:**
- Modify: `src/WinAppProfiles.UI/Views/CardWindow.xaml` — the icon picker `ComboBox` around line 828

**Context:** The icon picker `ComboBox` uses inline `Background`/`Foreground`/`BorderBrush` overrides instead of `ProfileComboBoxStyle`. This causes it to render with the native Windows ComboBox template (light grey background, Windows-blue highlight).

**Step 1: Find the icon picker ComboBox**

Find (around line 828):

```xml
<ComboBox ItemsSource="{Binding AvailableIconOptions}"
          SelectedItem="{Binding EditSelectedIconOption, Mode=TwoWay}"
          Background="{StaticResource BackgroundQuaternary}"
          Foreground="{StaticResource TextPrimary}"
          BorderBrush="{StaticResource BackgroundQuaternary}"
          MaxDropDownHeight="260">
    <ComboBox.Style>
        <Style TargetType="ComboBox">
            <Style.Triggers>
                <DataTrigger Binding="{Binding AvailableIconOptions.Count}" Value="0">
                    <Setter Property="Visibility" Value="Collapsed"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </ComboBox.Style>
```

**Step 2: Replace with styled version**

Replace with:

```xml
<ComboBox ItemsSource="{Binding AvailableIconOptions}"
          SelectedItem="{Binding EditSelectedIconOption, Mode=TwoWay}"
          Style="{StaticResource ProfileComboBoxStyle}"
          ItemContainerStyle="{StaticResource ProfileComboBoxItemStyle}"
          MaxDropDownHeight="260">
    <ComboBox.Visibility>
        <Binding Path="AvailableIconOptions.Count">
            <Binding.Converter>
                <BooleanToVisibilityConverter/>
            </Binding.Converter>
        </Binding>
    </ComboBox.Visibility>
```

Wait — the visibility is controlled by a `DataTrigger` on Count==0. `BooleanToVisibilityConverter` won't work directly on an int. Keep the visibility as a Style trigger. The cleanest approach is to use a `Style` that both applies `ProfileComboBoxStyle` as a `BasedOn` and adds the visibility trigger:

```xml
<ComboBox ItemsSource="{Binding AvailableIconOptions}"
          SelectedItem="{Binding EditSelectedIconOption, Mode=TwoWay}"
          ItemContainerStyle="{StaticResource ProfileComboBoxItemStyle}"
          MaxDropDownHeight="260">
    <ComboBox.Style>
        <Style TargetType="ComboBox" BasedOn="{StaticResource ProfileComboBoxStyle}">
            <Style.Triggers>
                <DataTrigger Binding="{Binding AvailableIconOptions.Count}" Value="0">
                    <Setter Property="Visibility" Value="Collapsed"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </ComboBox.Style>
```

**Step 3: Build to verify**

```bash
dotnet build src/WinAppProfiles.UI/WinAppProfiles.UI.csproj -c Debug
```

Expected: `Build succeeded.`

**Step 4: Commit**

```bash
git add src/WinAppProfiles.UI/Views/CardWindow.xaml
git commit -m "fix: apply ProfileComboBoxStyle to item settings icon picker"
```

---

### Task 5: Fix the dead space layout

**Files:**
- Modify: `src/WinAppProfiles.UI/Views/CardWindow.xaml` — the `StackPanel` inside the `ScrollViewer` in the drawer (around line 715)

**Context:** The `ScrollViewer` row has `Height="*"` which is correct (it pins the footer to the bottom). The problem is the `StackPanel` inside it has no vertical alignment set, so WPF stretches it to fill the `ScrollViewer`'s height — creating blank space below the content. Setting `VerticalAlignment="Top"` makes the content pack naturally while the footer stays pinned.

**Step 1: Find the StackPanel inside the ScrollViewer**

Find (around line 714):

```xml
<ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
    <StackPanel Margin="0,8,0,0">
```

**Step 2: Add VerticalAlignment**

Replace with:

```xml
<ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
    <StackPanel Margin="0,8,0,0" VerticalAlignment="Top">
```

**Step 3: Build to verify**

```bash
dotnet build src/WinAppProfiles.UI/WinAppProfiles.UI.csproj -c Debug
```

Expected: `Build succeeded.`

**Step 4: Visual verification**

```bash
pwsh scripts/run-debug.ps1
```

1. Open a profile in Card view
2. Click the gear icon on any card to open Item Settings
3. Verify: panel background is darker (`#1A1A20`), left border is visible, checkboxes are styled (accent square + tick), icon picker matches the Desired State dropdown style, no dead space between ICON section and footer buttons
4. Check that the disabled state works: set Desired State to "Stopped" or "Ignore", verify Force Minimized checkbox dims to 40% opacity

**Step 5: Commit**

```bash
git add src/WinAppProfiles.UI/Views/CardWindow.xaml
git commit -m "fix: remove dead space in item settings panel scrollable content"
```

---

### Task 6: Final branch push

```bash
git push -u origin HEAD
```

Then open a PR targeting `dev` with the summary: fixes 5 design system violations in the Item Settings drawer (custom checkboxes, correct panel background/border, styled icon picker, layout dead space).
