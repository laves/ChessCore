# PicoChess

This is a fork of the amazing [ChessCore](https://github.com/3583Bytes/ChessCore) project by Adam Berent. A voice command interface was added using [Picovoice's](https://picovoice.ai/) end-to-end platform.

## Usage
From the build directory, run:
```bash
dotnet ChessCore.dll
```

**NOTE**: To see the UTF-8 chess pieces in the Windows Command Prompt, you need to change your font to one that supports the chess piece characters. Both _MS Gothic_ or _NSimSun_ should work.

## Voice Commands

This game uses [descriptive chess notation](https://en.wikipedia.org/wiki/Descriptive_notation) for movement. A movement command is given in the format "Move [_source_] to [_destination_]", e.g:

> "Move queen's bishop one to king three."

In movement commands, the words "move" and "to" are optional.

Other commands:
-   "Undo last move."
-   "New game"
-   "Quit game"


## About ChessCore (by the author)

This project is a product of a rather rash decision in mid 2008 to learn to program my own Chess Game, hence began my journey into the art of computer chess.  The main goal of the original project was to learn about how computers play chess while producing a chess engine that is easily understood and well documented.  I feel that goal has now been achieved.  As the next step I decided to release the full source code for my chess engine under the MIT license to allow other developers to learn and contribute to further improve & extend my chess engine.

The documentation & tutorial on how to build a chess engine can be accessed in the following 2 formats:

PDF

http://www.adamberent.com/wp-content/uploads/2019/02/GuideToProgrammingChessEngine.pdf

Website

http://adamberent.com/home/chess/computer-chess/




