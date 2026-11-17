# Remote Keyboard Grid Builder

A Windows editor for making HTML layouts for [Remote Keyboard](https://github.com/Kellenes/Remote-Keyboard).

## Create a layout

1. Run `RemoteKeyboarGridBuilder.exe`. The editor opens with the Classic layout.
2. Enter a layout name. Turn off **Text input line** if you want a page with buttons only.
3. Use **Add row** to add a row. Select a cell in the preview and use **Add key** or **Add spacer** to add to that row. Each row holds up to four cells. A spacer leaves an empty spot.
4. Select a button to edit it. **Label** is the text shown on the button. Use the key dropdown or click **Press a key...** and press the key you want to send. **Width weight** makes a cell wider or narrower relative to the other cells in its row.
5. Click **Save HTML...** and save the file directly in the controller’s `wwwroot` folder, next to `Classic.html` if it is present.
6. Refresh the Remote Keyboard page and select your layout from the dropdown. The dropdown uses the filename without `.html`.

The editor exports HTML and starts with Classic each time. It cannot reopen an exported file for editing.

The single-file Windows build requires the [.NET 9 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0).
