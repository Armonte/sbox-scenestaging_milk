using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor;

/// <summary>
/// Global keybind helper that shows all shortcuts when F1 is pressed
/// </summary>
public static class KeybindHelper
{
	static ContextMenu _currentMenu;

	[Shortcut( "editor.show-keybinds", "F1" )]
	public static void ShowKeybindsMenu()
	{
		// If menu is already open, close it instead of opening another
		if ( _currentMenu != null && _currentMenu.IsValid() )
		{
			_currentMenu.Close();
			_currentMenu = null;
			return;
		}

		_currentMenu = new ContextMenu();
		_currentMenu.Searchable = true;
		_currentMenu.ToolTipsVisible = true;
		_currentMenu.AboutToHide += () => _currentMenu = null;

		BuildShortcutsMenu( _currentMenu );

		_currentMenu.OpenAtCursor( false );
	}

	static void BuildShortcutsMenu( ContextMenu menu )
	{
		// Group shortcuts by category
		var groups = EditorShortcuts.Entries
			.GroupBy( x => x.Group )
			.OrderBy( x => x.Key );

		int totalShortcuts = 0;

		foreach ( var group in groups )
		{
			// Only show groups that have visible shortcuts
			var shortcuts = group
				.GroupBy( x => x.Identifier )
				.Select( x => x.First() )
				.Where( x => !string.IsNullOrEmpty( EditorShortcuts.GetKeys( x.Identifier ) ) )
				.OrderBy( x => x.Name )
				.ToList();

			if ( shortcuts.Count == 0 )
				continue;

			// Create submenu for this category
			var categoryName = group.Key;
			var submenu = menu.AddMenu( categoryName, "folder" );

			// Add shortcuts to submenu
			AddShortcutsToMenu( submenu, shortcuts );

			totalShortcuts += shortcuts.Count;
		}
	}

	static void AddShortcutsToMenu( Menu menu, List<EditorShortcuts.Entry> shortcuts )
	{
		menu.ToolTipsVisible = true;

		foreach ( var shortcut in shortcuts )
		{
			var keys = EditorShortcuts.GetKeys( shortcut.Identifier );

			// Use tab character for alignment
			var displayText = $"{shortcut.Name}\t{keys}";
			var option = menu.AddOption( displayText, null, null );
			option.Enabled = false;
		}
	}
}
