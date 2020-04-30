using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Newtonsoft.Json.Linq;
using NDream.AirConsole;
using Crosstales.RTVoice.Tool;
using TMPro;


public class GameLogic : MonoBehaviour
{
    [SerializeField] bool testAutoPlay = false;
    [SerializeField] bool testFastMode = false;
    [SerializeField] int minPlayers = 2;
    [SerializeField] float callWait = 5f;
    [SerializeField] float ballAnimationSpeed = .5f;
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Color bColor;
    [SerializeField] Color iColor;
    [SerializeField] Color nColor;
    [SerializeField] Color gColor;
    [SerializeField] Color oColor;

    [SerializeField] SpeechText speechText;
    [SerializeField] Transform ballPositionTransform;
    [SerializeField] GameObject ballPrefab;
    [SerializeField] GameObject winMessage;
    [SerializeField] TextMeshProUGUI congratsMessage;

    [SerializeField] Dictionary<int, string> names = new Dictionary<int, string>();


    GameObject prevBall;
    GameObject nextBall;

    bool gameStarted = false;
    bool gameOver = false;

    Vector3 targetBallPosition;
    Color[] ballColors;
    Stack<JToken> cards;

    List<int> callNumbers;
    int callIndex = 0;

    private void Awake()
    {
        cards = new Stack<JToken>(GenerateBingoCards());
        AirConsole.instance.onConnect += OnConnect;
        AirConsole.instance.onMessage += OnMessage;
        targetBallPosition = ballPositionTransform.position;

        if (testFastMode) {
            callWait /= 10;
        }
    }

    void OnConnect(int device) {
        if (gameOver)
            return;
        //Add option for too many players
        AssignCard(device, cards.Pop());
    }

    void OnMessage(int deviceId, JToken data) {
        if (deviceId != 0)
        {
            int msgType = data.Value<int>("message_type");
            if (msgType == MessageTypes.BINGO) {
                ActivateBingo(deviceId);
                return;
            }
            if (msgType == MessageTypes.SET_NAME)
            {
                SetName(deviceId, data.Value<string>("name"));
                return;
            }
        }
    }

    void SetName(int deviceId, string name) {
        names[deviceId] = name;
        if (!gameStarted && names.Count >= minPlayers)
        {
            StartGame();
        }
    }

    void ActivateBingo(int deviceId) {
        if (gameOver)
            return;
        AirConsole.instance.Broadcast(new
        {
            message_type = MessageTypes.SET_GAME_STATE,
            state = GameStates.GAME_OVER
        });
        gameOver = true;
        StopAllCoroutines();
        Destroy(nextBall);
        Destroy(prevBall);
        speechText.Text = "BINGO!";
        speechText.Speak();
        winMessage.SetActive(true);
        congratsMessage.gameObject.SetActive(true);
        congratsMessage.text = $"Congratulations {names[deviceId]}";
    }

    void StartGame() {
        gameStarted = true;
        AirConsole.instance.Broadcast(new
        {
            message_type = MessageTypes.SET_GAME_STATE,
            state = GameStates.PLAYING,
            autoplay = testAutoPlay
        });
        ballColors = new Color[] {
            bColor,
            iColor,
            nColor,
            gColor,
            oColor
        };
        callNumbers = Enumerable.Range(1, 74).ToList();
        callNumbers.Shuffle(10);
        StartCoroutine(CallNumbers());
    }

    IEnumerator CallNumbers() {
        WaitForSeconds wait = new WaitForSeconds(callWait);
        while (true)
        {
            CallNextNumber();
            yield return wait;
        }
    }

    void CallNextNumber() {
        int screenWidth = 15;
        Vector3 screenWidthVector = new Vector3(screenWidth, 0f, 0f);

        if (prevBall != null) {
            LeanTween.move(prevBall, targetBallPosition + screenWidthVector, ballAnimationSpeed).setEase(LeanTweenType.easeOutCirc).setDestroyOnComplete(true);
        }
        nextBall = Instantiate(ballPrefab, targetBallPosition - screenWidthVector, Quaternion.identity);
        nextBall.transform.SetParent(ballPositionTransform.transform.parent, false);
        callIndex = (callIndex + 1) % callNumbers.Count;
        int callNumber = callNumbers[callIndex];
        int column = DetermineColumn(callNumber);
        string ballText = "BINGO"[column] + callNumber.ToString();
        nextBall.GetComponentInChildren<Image>().color = ballColors[column];
        Text txt = nextBall.GetComponentInChildren<Text>();
        txt.text = ballText;
        txt.color = textColor;
        LeanTween.move(nextBall, targetBallPosition, ballAnimationSpeed).setEase(LeanTweenType.easeInCirc).setOnComplete(() => {
            prevBall = nextBall;
            speechText.Delay = 0;
            speechText.Text = ballText;
            speechText.Rate = testFastMode ? 3f : 1f;
            speechText.Speak();
        });
        AirConsole.instance.Broadcast(new {
            message_type = MessageTypes.NUMBER_CALL,
            number = callNumber
        });
    }

    int DetermineColumn(int callNumber) {
        for (int n = 0; n < 4; n++)
        {
            if (callNumber < (n + 1) * 15)
            {
                return n;
            }
        }
        return 4;
    }

    void AssignCard(int device, JToken data) {
        data["message_type"] = MessageTypes.CARD_ASSIGNMENT;
        AirConsole.instance.Message(device, data);
    }

    List<JToken> GenerateBingoCards() {
        List<JToken> result = new List<JToken>();

        for (int set = 0; set < 5; set++)
        {
            //Generate stack of random numbers for each column
            List<Stack<int>> columnStacks = new List<Stack<int>>();
            for (int column = 0; column < 5; column++)
            {
                List<int> columnList = Enumerable.Range(column * 15 + 1, 15).ToList();
                columnList.Shuffle(15);
                columnStacks.Add(new Stack<int>(columnList));
            }

            //For each card
            for (int card = 0; card < 3; card++)
            {
                //Generate grid of numbers by popping column stacks for each column
                int[,] cardNumbers = new int[5, 5];
                for (int column = 0; column < columnStacks.Count; column++)
                {
                    for (int row = 0; row < 5; row++)
                    {
                        cardNumbers[column, row] = columnStacks[column].Pop();
                    }
                }

                JObject cardData = JObject.FromObject(new
                {
                    numbers = cardNumbers
                });
                result.Add(cardData);
            }
        }
        return result;
    }
}

static class MessageTypes {
    public const int CARD_ASSIGNMENT = 0;
    public const int NUMBER_CALL = 1;
    public const int BINGO = 2;
    public const int SET_NAME = 3;
    public const int SET_SCREEN = 4;
    public const int SET_WIN_STAGE = 5;
    public const int SET_GAME_STATE = 6;
}

static class GameStates {
    public const int WAITING_ON_PLAYERS = 0;
    public const int PLAYING = 1;
    public const int GAME_OVER = 2;
}


public static class IListExtensions
{
    /// <summary>
    /// Shuffles the element order of the specified list.
    /// </summary>
    public static void Shuffle<T>(this IList<T> ts, int shuffleCount = 1)
    {
        var count = ts.Count;
        var last = count - 1;
        for (int n = 0; n < shuffleCount; n++) {
            for (var i = 0; i < last; ++i)
            {
                var r = UnityEngine.Random.Range(i, count);
                var tmp = ts[i];
                ts[i] = ts[r];
                ts[r] = tmp;
            }
        }
    }
}

