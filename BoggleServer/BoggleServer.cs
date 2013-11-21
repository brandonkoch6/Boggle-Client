using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using BB;
using CustomNetworking;
using System.Threading;
using System.Timers;


namespace BB
{
    class BoggleServer
    {
        #region Implementation Thoughts
        //args will be passed two, possibly three command line parameters
        // The time in seconds the game should last
        // Pathname of a file containing all legal words
        // An optional string of 16 characters that can be used to initialize a boggle board

        //Create a string socket and wait for a connection to be established

        #endregion

        #region Globals

        // Listens for incoming connectoin requests
        private TcpListener boggleServer;

        // Encoding used for incoming/outgoing data
        private static System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();

        // Queue for holding latest two String Sockets that come in
        Queue<Player> playerQueue;

        // Locks
        private readonly Object playerQueueLock;

        // 
        public int gameTime;
        private string dictionaryFilepath;
        private string optionalBoggleBoard;

        #endregion

        /// <summary>
        /// Receives argument from user.  Requires 2 - 3 arguments to begin boggle game,
        /// otherwise it will throw an exception.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            args = new String[]{"20", "dictionary.txt", ""};

            // Check to see that the appropriate number of arguments has been passed
            // to the server via the string[] args
            if (args.Length == 2)
            {
                 new BoggleServer(args[0], args[1], "");

                 // Keep the main thread active so we can see output to the console
                 Console.ReadLine();
            }
            else if(args.Length == 3)
            {
                new BoggleServer(args[0], args[1], args[2]);

                // Keep the main thread active so we can see output to the console
                Console.ReadLine();
            }
            else
                throw new Exception();
        }

        /// <summary>
        /// Constructor that creates a BoggleServer that listens for connection requests on port 2000
        /// </summary>
        public BoggleServer(string gameLength, string dictionaryFile, string customBoard)
        {
            // Assign to global variables:
            int.TryParse(gameLength, out gameTime);
            this.dictionaryFilepath = dictionaryFile;

            // Check to see if the optionalBoard exists and if it is 16 characters
            if (customBoard.Length == 16)
            {
                this.optionalBoggleBoard = customBoard;
            }
            else
            {
                this.optionalBoggleBoard = "";
            }
            
            // Instantiate variables
            playerQueue = new Queue<Player>();
            playerQueueLock = new Object();

            // A TcpListener listening for any incoming connections
            boggleServer = new TcpListener(IPAddress.Any, 2000);

            // Start the TcpListener
            boggleServer.Start();

            // Ask our new boggle server to call a specific method once a connection arrives
            // the waiting and calling will happen on another thread.  This call will return immediately
            // and the constructor will return to main
            boggleServer.BeginAcceptSocket(ConnectionRequested, null);
        }

        /// <summary>
        /// This method is called when boggleServer.BeginAcceptSocket receives an incoming connection
        /// to the server
        /// </summary>
        /// <param name="result"></param>
        public void ConnectionRequested(IAsyncResult result)
        {
            // Obtain the socket corresponding to the incoming request.
            Socket s = boggleServer.EndAcceptSocket(result);

            // Should probably use a StringSocket here...
            StringSocket ss = new StringSocket(s, encoding);

            // Start listening to that player
            ss.BeginReceive(messageRetreived,ss);

            // Send them a welcome message
            ss.BeginSend("Welcome To Our Boggle Server \r\n", (e, o) => { }, 2);

            // Start listening for more incoming connections
            boggleServer.BeginAcceptSocket(ConnectionRequested, null);
        }

        /// <summary>
        /// This method should enqueue a player in our player pool if they have
        /// requested to play with a player name and place them on a new thread
        /// with a game if there exists a second player on the queue.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        /// <param name="payload"></param>
        private void messageRetreived(string s, Exception e, object payload)
        {
            // Cast the payload as a String Socket
            StringSocket currentSS = (StringSocket) payload;

            // Check if they asked to play
            if (s.StartsWith("play ", true, null))
            {
                // Create a new player with the name and stringsocket
                Player tempPlayer = new Player(currentSS, s.Substring(4).Trim());

                // Now that the player is ready, place them in the queue
                playerQueue.Enqueue(tempPlayer);

                currentSS.BeginSend("Waiting for another player... \r\n", (exc, o) => { }, currentSS);
            }
                // If we didn't receive a play command then keep listening for additional commands
            else
            {
                currentSS.BeginReceive(messageRetreived, currentSS);
            }

            // if there are two people wanting to play a game one with another
            // via the boggle server then pair them up, pass both String Sockets to a method
            // and begin a game of boggle!
            while (playerQueue.Count >= 2)
            {
                // Dequeue both StringSockets and get that game goin!
                lock (playerQueueLock)
                {
                    // Create a new game
                    Game g = new Game(playerQueue.Dequeue(), playerQueue.Dequeue(), optionalBoggleBoard, this.gameTime);

                    // Begin a new game on a new thread
                    Thread t = new Thread(new ThreadStart(g.GameOn));
                    t.Start();
                }
            }
        }

        #region Helper Classes

        /// <summary>
        ///  This encapsulates two players and a method to begin the game
        /// </summary>
        private class Game
        {
            // Globals
            Player playerOne;
            Player playerTwo;
            string optionalBoggleBoard;
            BoggleBoard b;
            int gameTime;
            System.Timers.Timer timer;
            int timeCount;

            public Game(Player player1, Player player2, string _optionalBoggleBoard, int _gameTime)
            {
                playerOne = player1;
                playerTwo = player2;
                optionalBoggleBoard = _optionalBoggleBoard;
                gameTime = _gameTime;
                timer = new System.Timers.Timer(1000);
                timeCount = 0;
            }

            /// <summary>
            /// Controls a game of boggle between two players
            /// </summary>
            /// <param name="player1"></param>
            /// <param name="player2"></param>
            public void GameOn()
            {
                // If the server has a custom board then be sure to use it
                if (optionalBoggleBoard == String.Empty)
                {
                    b = new BoggleBoard();
                }
                else
                {
                    b = new BoggleBoard(optionalBoggleBoard);
                }
 
                //Send some sort of welcome message
                playerOne.StringSocketReceive.BeginSend("A Game is afoot!\r\n", (e, o) => { }, 2);
                playerTwo.StringSocketReceive.BeginSend("A Game is afoot!\r\n", (e, o) => { }, 2);

                // Send the starting information
                /* 
                 * Once the server has received connections from two clients that are ready to play, 
                 * it pairs them in a game. The server begins the game by sending a command to each client. 
                 * The command is "START $ # @", where $ is the 16 characters that appear on the Boggle board 
                 * being used for this game, # is the length of the game in seconds, and @ is the opponent's name.
                 * The length of the game should be the value that was passed to the server as a command line parameter. 
                 * The board configuration should be chosen at random, unless a configuration was passed to the server as 
                 * its optional command line parameter.
                 */

                playerOne.StringSocketReceive.BeginSend("\nSTART " + b.ToString() + " " + gameTime + " " + playerTwo.getName + "\r\n", (e, o) => { }, 2);
                playerTwo.StringSocketReceive.BeginSend("\nSTART " + b.ToString() + " " + gameTime + " " + playerOne.getName + "\r\n", (e, o) => { }, 2);

                timer.Elapsed += timeElapsed;
                timer.Start();

                // Start listening for inputs
                playerOne.StringSocketReceive.BeginReceive(gameMessageReceived, playerOne.StringSocketReceive);
                playerTwo.StringSocketReceive.BeginReceive(gameMessageReceived, playerTwo.StringSocketReceive);
                

            }

            private void gameMessageReceived(string s, Exception e, object payload)
            {
                StringSocket tempSS = (StringSocket)payload;

                Console.WriteLine(s);

                tempSS.BeginReceive(gameMessageReceived, tempSS);

            }

            #region Game Helper Methods

            /// <summary>
            /// Method used to invoke the timer
            /// </summary>
            /// <param name="state"></param>
            private void timeElapsed(object sender, ElapsedEventArgs e)
            {
                // Advance the timer one second
                timeCount = timeCount + 1;

                // Notify the players a second has passed
                playerOne.StringSocketReceive.BeginSend("TIME "+ (timeCount) +"\r\n", (exc, o) => { }, 2);
                playerTwo.StringSocketReceive.BeginSend("TIME " + (timeCount) + "\r\n", (exc, o) => { }, 2);

                // If we have run out of time then end the game
                if (timeCount == gameTime)
                {
                    EndGame();
                    timer.Stop();
                }
            }

            private void EndGame()
            {
                playerOne.StringSocketReceive.BeginSend("THE GAME HAS ENDED!\r\n", (exc, o) => { }, 2);
                playerTwo.StringSocketReceive.BeginSend("THE GAME HAS ENDED!\r\n", (exc, o) => { }, 2);
            }

            #endregion
        }

        /// <summary>
        /// Encapsulating class holding a players name and associated string socket
        /// </summary>
        private class Player
        {
            // Instance variables for a request:
            StringSocket ss;
            string name;

            /// <summary>
            /// Constructor requires a delegate which takes an exception and an arbitrary object as an identifier
            /// for the receive request as well as an Exception object.
            /// </summary>
            /// <param name="payloadReceive"></param>
            /// <param name="method"></param>
            public Player(StringSocket _ss, string _name)
            {
                this.ss = _ss;
                this.name = _name;

            }

            /// <summary>
            /// Property which returns the payload identifier.
            /// </summary>
            public StringSocket StringSocketReceive
            {
                get { return ss; }
            }

            /// <summary>
            /// Property which returns the request callback.
            /// </summary>
            public string getName
            {
                get { return name; }
            }


        }

        #endregion
    }      
}
