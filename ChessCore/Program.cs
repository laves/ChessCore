using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ChessEngine.Engine;
using OpenTK.Audio.OpenAL;
using Pv;

class Program
{
	private static readonly Engine engine = new Engine();
	private static string Platform => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac" :
												 RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
												 RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "";

	static void Main(string[] args)
	{
		Console.OutputEncoding = Encoding.Unicode;
		RunEngine();
	}

	private static void RunEngine()
	{		
		// init picovoice platform
		string keywordPath = $"./resources/keyword_files/{Platform}/picovoice_{Platform}.ppn";
		string contextPath = $"chess_{Platform}.rhn";

        static void wakeWordCallback()
		{			
			Console.WriteLine("\nListening for command...");
		}

        static void inferenceCallback(Inference inference)
		{
			if (inference.IsUnderstood)
			{ 
				string cmd = inference.Intent.ToLower();
				if (cmd.Equals("move"))
				{
					
					string srcSide = inference.Slots["src_side"].ToLower();
					string srcRank = inference.Slots["src_rank"].ToLower();
					string srcFile = inference.Slots.ContainsKey("src_file") ? inference.Slots["src_file"].ToLower() : "";

					string dstSide = inference.Slots["dst_side"].ToLower();
					string dstRank = inference.Slots["dst_rank"].ToLower();
					string dstFile = inference.Slots.ContainsKey("dst_file") ? inference.Slots["dst_file"].ToLower() : "";


					string playerMove = MakePlayerMove(srcSide, srcFile, srcRank, dstSide, dstFile, dstRank);
					if (playerMove.Equals("Invalid Move"))
					{
						Console.Clear();
						Console.WriteLine();
						Console.WriteLine($" {playerMove}");						
						DrawBoard();
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
			}
			else
			{
				Console.Clear();
				Console.WriteLine(" Didn't understand move.");
				Console.WriteLine();
				DrawBoard();								
			}			
		}

		using Picovoice picovoice = new Picovoice(keywordPath, wakeWordCallback, contextPath, inferenceCallback);

		DrawBoard();

		// create and start recording
		short[] recordingBuffer = new short[picovoice.FrameLength];
		ALCaptureDevice captureDevice = ALC.CaptureOpenDevice(null, 16000, ALFormat.Mono16, picovoice.FrameLength * 2);
		{
			ALC.CaptureStart(captureDevice);
			while (true)
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
			//Console.WriteLine("Stopping...");
			//ALC.CaptureStop(captureDevice);
			//ALC.CaptureCloseDevice(captureDevice);
		}	


	//			if (move == "new")
	//			{
	//				engine.NewGame();
	//				continue;
	//			}
	//			if (move == "quit")
	//			{
	//				return;
	//			}
	//			if (move == "undo")
	//			{
	//				engine.Undo();
	//				continue;
	//			}

	}

	private static string MakePlayerMove(string srcSide, string srcFile, string srcRank, 
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

		if (!engine.IsValidMove(srcCol, srcRow, dstCol, dstRow))
		{
			return "Invalid Move";	
		}

		engine.MovePiece(srcCol, srcRow, dstCol, dstRow);
		return $"{GetColumn(srcCol)} {GetRow(srcRow)} -> {GetColumn(dstCol)} {GetRow(dstRow)}";
	}

	private static string MakeOpponentMove()
	{
		engine.AiPonderMove();

		MoveContent lastMove = engine.GetMoveHistory().ToArray()[0];

		var srcCol = (byte)(lastMove.MovingPiecePrimary.SrcPosition % 8);
		var srcRow = (byte)(lastMove.MovingPiecePrimary.SrcPosition / 8);
		var dstCol = (byte)(lastMove.MovingPiecePrimary.DstPosition % 8);
		var dstRow = (byte)(lastMove.MovingPiecePrimary.DstPosition / 8);

		return $"{GetColumn(srcCol)} {GetRow(srcRow)} -> {GetColumn(dstCol)} {GetRow(dstRow)}";
	}

	private static bool CheckEndGame()
    {
		return engine.StaleMate || engine.GetWhiteMate() || engine.GetBlackMate();
	}

	private static string GetEndGameReason()
    {
		if (engine.StaleMate)
		{
            string reason;
            if (engine.InsufficientMaterial)
			{
				reason = "1/2-1/2 {Draw by insufficient material}";
			}
			else if (engine.RepeatedMove)
			{
				reason = "1/2-1/2 {Draw by repetition}";
			}
			else if (engine.FiftyMove)
			{
				reason = "1/2-1/2 {Draw by fifty move rule}";
			}
			else
			{
				reason = "1/2-1/2 {Stalemate}";
			}
			engine.NewGame();
			return reason;
		}
		else if (engine.GetWhiteMate())
		{			
			engine.NewGame();
			return "0-1 {Black mates}";
		}
		else if (engine.GetBlackMate())
		{			
			engine.NewGame();
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

			ChessPieceType PieceType = engine.GetPieceTypeAt(i);
			ChessPieceColor PieceColor = engine.GetPieceColorAt(i);			
			Console.Write($"| {GetPieceSymbol(PieceColor, PieceType)} ");

			if (i % 8 == 7)
			{
				Console.Write("|");
			}
		}

		Console.WriteLine();
		Console.WriteLine("  ---------------------------------  ");
		Console.WriteLine("    QR  QN  QB   Q  K   KB  KN  KR");
		Console.WriteLine();
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
