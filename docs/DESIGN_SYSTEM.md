# SAMA Design System

## Style

**Utilitarian with subtle warmth** - Burnt orange (`#CC5500`) accent color, Bootstrap 5 base, system fonts, Lucide icons.

## Colors

All colors are CSS variables in `theme.css`. Use Bootstrap 5 classes by default.

**Primary**: `#CC5500` (burnt orange) - buttons, links, active states  
**Status**: Green (up), Amber (degraded), Red (down), Cyan (info)
**Dark theme**: `[data-theme="dark"]` on `<html>`, toggled via JS + localStorage

## Icons

**Lucide web font** - Use class syntax: `<i class="icon-pencil icon-sm"></i>`

**Sizes**: `.icon-xs` (12px) → `.icon-3xl` (48px)

**Button icons (required)**:
- Edit: `icon-pencil`
- Delete: `icon-trash-2`
- Create: `icon-plus`
- Save: `icon-check`

**No icons needed**: Cancel buttons, filter toggles, dropdown items

See existing code for patterns.

