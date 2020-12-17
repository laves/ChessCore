using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ChessEngine.Engine;
using OpenTK.Audio.OpenAL;
using Pv;

class Program
{
	private static readonly Engine gameEngine = new Engine();
	private static string _platform => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac" :
												 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
												 RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "";
	private static bool _quitGame = false;

	static void Main(string[] _)
	{
		Console.OutputEncoding = Encoding.UTF8;
		RunGame();
	}

	private static void RunGame()
	{		
		// init picovoice platform
		string keywordPath = $"pico_chess_{_platform}.ppn";
		string contextPath = $"chess_{_platform}.rhn";        

		using Picovoice picovoice = new Picovoice(keywordPath, WakeWordCallback, contextPath, InferenceCallback);

		DrawBoard();

		// create and start recording
		short[] recordingBuffer = new short[picovoice.FrameLength];
		ALCaptureDevice captureDevice = ALC.CaptureOpenDevice(null, 16000, ALFormat.Mono16, picovoice.FrameLength * 2);
		{
			ALC.CaptureStart(captureDevice);
			while (!_quitGame)
			{
				int samplesAvailable = ALC.GetAvailableSamples(captureDevice);
				if (samplesAvailable > picovoice.FrameLength)
				{
					ALC.CaptureSamples(captureDevice, ref recordingBuffer[0], picovoice.FrameLength);
					picovoice.Process(recordingBuffer);

				}
				Thread.Yield();
			}

            // stop and clean up resources
            Console.WriteLine("Bye!");
            ALC.CaptureStop(captureDevice);
            ALC.CaptureCloseDevice(captureDevice);
        }	
	}

	static void WakeWordCallback()
	{
		Console.WriteLine("\n Listening for command...");
	}

	static void InferenceCallback(Inference inference)
	{
		if (inference.IsUnderstood)
		{
			string cmd = inference.Intent;
			if (cmd.Equals("move"))
			{
				string srcSide = inference.Slots["srcSide"];
				string srcRank = inference.Slots["srcRank"];
				string srcFile = inference.Slots.ContainsKey("srcFile") ? inference.Slots["srcFile"] : "";

				string dstSide = inference.Slots["dstSide"];
				string dstRank = inference.Slots["dstRank"];
				string dstFile = inference.Slots.ContainsKey("dstFile") ? inference.Slots["dstFile"] : "";

				string playerMove = MakePlayerMove(srcSide, srcFile, srcRank, dstSide, dstFile, dstRank);
				if (playerMove.Equals("Invalid Move"))
				{
					Console.Clear();
					Console.WriteLine();
					Console.WriteLine($" {playerMove}");
					DrawBoard();
					return;
				}

				string theirMove = MakeOpponentMove();

				Console.Clear();
				Console.WriteLine($" \x2654  {playerMove}");
				Console.WriteLine($" \x265A  {theirMove}");
				DrawBoard();

				if (CheckEndGame())
				{
					Console.WriteLine($"\n {GetEndGameReason()}");
					Console.WriteLine($" Say 'new game' to play again.");
				}
			}
			else if (cmd.Equals("undo"))
			{
				UndoLastMove();
			}
			else if (cmd.Equals("newgame"))
			{
				NewGame();
			}
			else if (cmd.Equals("quit"))
			{
				QuitGame();
			}
		}
		else
		{
			Console.Clear();
			Console.WriteLine(" Didn't understand move.");
			Console.WriteLine();
			DrawBoard();
		}
	}

	static void NewGame()
    {
		gameEngine.NewGame();
		Console.Clear();
		Console.WriteLine(" New game started");
		Console.WriteLine();
		DrawBoard();
	}

	static void UndoLastMove()
    {
		gameEngine.Undo();
		Console.Clear();
		Console.WriteLine(" Last move undid");
		Console.WriteLine();
		DrawBoard();
	}

	static void QuitGame()
    {
		Console.Clear();
		_quitGame = true;
	}


	static string MakePlayerMove(string srcSide, string srcFile, string srcRank, 
		string dstSide, string dstFile, string dstRank)
    {
		byte srcCol;
		byte srcRow;
		byte dstRow;
		byte dstCol;

		try
		{
			srcCol = GetColumn(srcSide, srcFile);
			srcRow = GetRow(srcRank);
			dstCol = GetColumn(dstSide, dstFile);
			dstRow = GetRow(dstRank);

		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			return "Invalid Move";
		}

		if (!gameEngine.IsValidMove(srcCol, srcRow, dstCol, dstRow))
		{
			return "Invalid Move";	
		}

		gameEngine.MovePiece(srcCol, srcRow, dstCol, dstRow);
		return $"{GetColumn(srcCol)} {GetRow(srcRow)} -> {GetColumn(dstCol)} {GetRow(dstRow)}";
	}

	private static string MakeOpponentMove()
	{
		gameEngine.AiPonderMove();

		MoveContent lastMove = gameEngine.GetMoveHistory().ToArray()[0];

		var srcCol = (byte)(lastMove.MovingPiecePrimary.SrcPosition % 8);
		var srcRow = (byte)(lastMove.MovingPiecePrimary.SrcPosition / 8);
		var dstCol = (byte)(lastMove.MovingPiecePrimary.DstPosition % 8);
		var dstRow = (byte)(lastMove.MovingPiecePrimary.DstPosition / 8);

		return $"{GetColumn(srcCol)} {GetRow(srcRow)} -> {GetColumn(dstCol)} {GetRow(dstRow)}";
	}

	private static bool CheckEndGame()
    {
		return gameEngine.StaleMate || gameEngine.GetWhiteMate() || gameEngine.GetBlackMate();
	}

	private static string GetEndGameReason()
    {
		if (gameEngine.StaleMate)
		{
            string reason;
            if (gameEngine.InsufficientMaterial)
			{
				reason = "1/2-1/2 {Draw by insufficient material}";
			}
			else if (gameEngine.RepeatedMove)
			{
				reason = "1/2-1/2 {Draw by repetition}";
			}
			else if (gameEngine.FiftyMove)
			{
				reason = "1/2-1/2 {Draw by fifty move rule}";
			}
			else
			{
				reason = "1/2-1/2 {Stalemate}";
			}
			gameEngine.NewGame();
			return reason;
		}
		else if (gameEngine.GetWhiteMate())
		{			
			gameEngine.NewGame();
			return "0-1 {Black mates}";
		}
		else if (gameEngine.GetBlackMate())
		{			
			gameEngine.NewGame();
			return "1-0 {White mates}";
		}
		else
		{
			return "Not end game";
		}
	}


	private static byte GetRow(string move)
	{
		return move switch
		{
			"one" => 7,
			"two" => 6,
			"three" => 5,
			"four" => 4,
			"five" => 3,
			"six" => 2,
			"seven" => 1,
			"eight" => 0,
			_ => 255
		};
	}

	private static string GetRow(byte row)
    {
		return row switch
		{
			7 => "one",
			6 => "two",
			5 => "three",
			4 => "four",
			3 => "five",
			2 => "six",
			1 => "seven",
			0 => "eight",
			_ => "?"
		};
    }

	private static string GetColumn(byte col)
	{
		return col switch
		{
			7 => "king's rook",
			6 => "king's knight",
			5 => "king's bishop",
			4 => "king",
			3 => "queen",
			2 => "queen's bishop",
			1 => "queen's knight",
			0 => "queen's rook",
			_ => "?"
		};
	}

	private static byte GetColumn(string side, string file)
	{
		if (side.Equals("queen") || side.Equals("queen's"))
		{
			if (file.Equals("rook"))
				return 0;
			else if (file.Equals("knight"))
				return 1;
			else if (file.Equals("bishop"))
				return 2;
			else
				return 3;
		}
		else if (side.Equals("king") || side.Equals("king's"))
		{
			if (file.Equals("rook"))
				return 7;
			else if (file.Equals("knight"))
				return 6;
			else if (file.Equals("bishop"))
				return 5;
			else
				return 4;
		}
        else
        {
			return 255;
        }
	}


	private static void DrawBoard()
	{		
		for (byte i = 0; i < 64; i++)
		{
			if (i % 8 == 0)
			{
				Console.WriteLine();
				Console.WriteLine("  ---------------------------------");
				Console.Write(" " + (8 - (i / 8)));
			}

			ChessPieceType PieceType = gameEngine.GetPieceTypeAt(i);
			ChessPieceColor PieceColor = gameEngine.GetPieceColorAt(i);			
			Console.Write($"| {GetPieceSymbol(PieceColor, PieceType)} ");

			if (i % 8 == 7)
			{
				Console.Write("|");
			}
		}

		Console.WriteLine();
		Console.WriteLine("  ---------------------------------  ");
		Console.WriteLine("    QR  QN  QB  Q   K   KB  KN  KR");
		Console.WriteLine();
		Console.WriteLine(" Say 'PicoChess' followed by a command.\n Available commands are:");
		Console.WriteLine("   - '(move) <src> (to) <dst>'");
		Console.WriteLine("   - 'undo last move'");
		Console.WriteLine("   - 'new game'");
		Console.WriteLine("   - 'quit game'");
	}

	private static string GetPieceSymbol(ChessPieceColor color, ChessPieceType type)
	{
        return color switch
        {
            ChessPieceColor.White => type switch
            {
                ChessPieceType.King => "\u2654",
                ChessPieceType.Queen => "\u2655",
                ChessPieceType.Rook => "\u2656",
                ChessPieceType.Bishop => "\u2657",
                ChessPieceType.Knight => "\u2658",
                ChessPieceType.Pawn => "\u2659",
                _ => " "
            },
            ChessPieceColor.Black => type switch
            {
                ChessPieceType.King => "\u265A",
                ChessPieceType.Queen => "\u265B",
                ChessPieceType.Rook => "\u265C",
                ChessPieceType.Bishop => "\u265D",
                ChessPieceType.Knight => "\u265E",
                ChessPieceType.Pawn => "\u265F",
                _ => " "
            },
            _ => " ",
        };
    }
}
