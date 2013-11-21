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
        private int gameTime;
        private static HashSet<string> dictionaryFile;
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

            // If file path is not valid, throw exception.
            if (!System.IO.File.Exists(args[1]))
                throw new Exception("Invalid dictionary file path.");
            
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
        public BoggleServer(string gameLength, string dictionaryFilePath, string customBoard)
        {
            // Assign to global variables:
            int.TryParse(gameLength, out gameTime);

            try
            {
                // Create a dictionary set of words based on the filepath of the dictionary.
                string[] lines = System.IO.File.ReadAllLines(dictionaryFilePath);
                foreach (string line in lines)
                {
                    dictionaryFile.Add(line);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Issues parsing dictionary file");
            }

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
                // The client has deviated from the protocol - send IGNORING message.
                currentSS.BeginSend("IGNORING" + s, (exc, o) => { }, 2);
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
                playerOne.StringSocket.BeginSend("A Game is afoot!\r\n", (e, o) => { }, 2);
                playerTwo.StringSocket.BeginSend("A Game is afoot!\r\n", (e, o) => { }, 2);

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

                playerOne.StringSocket.BeginSend("\nSTART " + b.ToString() + " " + gameTime + " " + playerTwo.Name + "\r\n", (e, o) => { }, 2);
                playerTwo.StringSocket.BeginSend("\nSTART " + b.ToString() + " " + gameTime + " " + playerOne.Name + "\r\n", (e, o) => { }, 2);

                // Will invoke timeElapse every 1000 milliseconds
                timer.Elapsed += timeElapsed;
                timer.Start();

                // Start listening for inputs
                playerOne.StringSocket.BeginReceive(gameMessageReceived, playerOne);
                playerTwo.StringSocket.BeginReceive(gameMessageReceived, playerTwo);
                

            }

            private void gameMessageReceived(string s, Exception e, object payload)
            {

                Console.WriteLine(s);

                Player readyPlayer = (Player)payload;

                // Assume the opponent is playerOne
                Player opponent = playerOne;

                // If the readyPlayer is player one, change
                // the opponent to playerTwo.
                if (readyPlayer.Equals(playerOne))
                {
                    opponent = playerTwo;
                }

                // Check if the cmd line input was valid
                if (s.StartsWith("word "))
                {
                    string word = s.Substring(4).Trim();

                    bool isLegal = (dictionaryFile.Contains(word) && b.CanBeFormed(word));

                    if (word.Length > 2)
                    {
                        // For any word that appears more than once, all but the first occurrence is removed (whether or not it is legal).
                        if (!readyPlayer.LegalWords.Contains(word) && !readyPlayer.DuplicateWords.Contains(word) && !opponent.LegalWords.Contains(word) && !opponent.DuplicateWords.Contains(word))
                        {
                            // Add to the player's legal words
                            readyPlayer.LegalWords.Add(word);

                            // Increment player's score.
                            readyPlayer.Score += AddScore(word);
                        }
                        // Each legal word that occurs on the opponent's list is removed.
                        // Each remaining illegal word is worth negative one points.
                        // Each remaining legal word earns a score that depends on its length.  Three and four-letter words are worth
                        // one point, five-letter words are worth two points, six-letter words are worth three points, seven-letter words
                        // are worth five points, and longer word are worth 11 points.


                        // Legal - CanBeFormed, >2 letters
                            
                            // Is it not in duplicates list and in our legal words list?
                                
                                // Is it a duplicate?

                                        // ADd to duplicate, subtract from opponent list and score
                                // If not, we can add

                        // Do nothing
                    // Illegal
                           // subtract points

                        
                    }

                    // If the message is valid and it has greater than two characters
                    if (b.CanBeFormed(s) && s.Length > 2)
                    {
                        // Add it to this player's set of words if it doesn't exist in the other player's set.
                        if (!opponent.words.Contains(s))
                        {
                            readyPlayer.words.Add(s);
                        }



                    }
                }
                else
                {
                    // The client has deviated from the protocol - send IGNORING message.
                    readyPlayer.StringSocket.BeginSend("IGNORING" + s, (exc, o) => { }, 2);
                }

                readyPlayer.StringSocket.BeginReceive(gameMessageReceived, readyPlayer);


            }

            #region Game Helper Methods

            /// <summary>
            /// Returns a positive value for a legal word.
            /// </summary>
            /// <param name="state"></param>
            private int AddScore(string word)
            {
                // Each remaining legal word earns a score that depends on its length.  Three and four-letter words are worth
                // one point, five-letter words are worth two points, six-letter words are worth three points, seven-letter words
                // are worth five points, and longer word are worth 11 points.
                if (word.Length == 3 || word.Length == 4)
                    return 1;
                else if (word.Length == 5)
                    return 2;
                else if (word.Length == 6)
                    return 3;
                else if (word.Length == 7)
                    return 5;
                else
                    return 11;
            }

            /// <summary>
            /// Returns a negative value for an illegal word.
            /// </summary>
            /// <param name="state"></param>
            private int SubScore(string word)
            {
                // Subtract points from the player due to duplicates.
                if (word.Length == 3 || word.Length == 4)
                    return -1;
                else if (word.Length == 5)
                    return -2;
                else if (word.Length == 6)
                    return -3;
                else if (word.Length == 7)
                    return -5;
                else
                    return -11;
            }

            /// <summary>
            /// Method used to invoke the timer
            /// </summary>
            /// <param name="state"></param>
            private void timeElapsed(object sender, ElapsedEventArgs e)
            {
                // Advance the timer one second
                timeCount = timeCount + 1;

                // Notify the players a second has passed
                playerOne.StringSocket.BeginSend("TIME "+ (timeCount) +"\r\n", (exc, o) => { }, 2);
                playerTwo.StringSocket.BeginSend("TIME " + (timeCount) + "\r\n", (exc, o) => { }, 2);

                // If we have run out of time then end the game
                if (timeCount == gameTime)
                {
                    EndGame();
                    timer.Stop();
                }
            }

            private void EndGame()
            {
                playerOne.StringSocket.BeginSend("THE GAME HAS ENDED!\r\n", (exc, o) => { }, 2);
                playerTwo.StringSocket.BeginSend("THE GAME HAS ENDED!\r\n", (exc, o) => { }, 2);
            }

            #endregion
        }

        /// <summary>
        /// Encapsulating class holding a players name and associated string socket
        /// </summary>
        private class Player
        {
            // Instance variables for a player object:
            private StringSocket ss;
            private string name;
            private int score;
            private HashSet<string> legalWords;
            private HashSet<string> illegalWords;
            private HashSet<string> duplicateWords;

            /// <summary>
            /// Constructor requires a StringSocket object and the player's name.
            /// </summary>
            /// <param name="payloadReceive"></param>
            /// <param name="method"></param>
            public Player(StringSocket _ss, string _name)
            {
                // Initialize instance variables:
                this.ss = _ss;
                this.name = _name;
                this.legalWords = new HashSet<string>();
                this.illegalWords = new HashSet<string>();
                this.duplicateWords = new HashSet<string>();
                this.score = 0;

            }

            /// <summary>
            /// Property which returns the payload identifier.
            /// </summary>
            public StringSocket StringSocket
            {
                get { return ss; }
            }

            /// <summary>
            /// Property which returns the request callback.
            /// </summary>
            public string Name
            {
                get { return name; }
            }

            /// <summary>
            /// Property which returns the set of legal words played by this player.
            /// </summary>
            public HashSet<string> LegalWords
            {
                get { return legalWords; }
            }
            /// <summary>
            /// Property which returns the set of illegal words played by this player.
            /// </summary>
            public HashSet<string> IllegalWords
            {
                get { return illegalWords; }
            }
            /// <summary>
            /// Property which returns the duplicate words played by the player.
            /// </summary>
            public HashSet<string> DuplicateWords
            {
                get { return duplicateWords; }
            }

            /// <summary>
            /// Property which returns the player's score.
            /// </summary>
            public int Score
            {
                get { return score; }
                set { score = value; }
            }


        }

        #endregion
    }      
}
