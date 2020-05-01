var rateLimiter;
var airconsole;
var viewManager;
var gamestate = 0; //0: waiting to start, 1: playing, 2: gameover
var autoplay = false;

var name = "";
var numbersDict = {};
var cellsDict = {}; //For auto-play
var activeNumbers = [];
var maxActiveNumbers = 3;

var activeTokens = [
  [false, false, false, false, false],
  [false, false, false, false, false],
  [false, false, false, false, false],
  [false, false, false, false, false],
  [false, false, false, false, false]
];
var bingoReady = false;

function App() {
  airconsole = new AirConsole({
    "orientation": "portrait",
    "synchronize_time": "true"
  });
  rateLimiter = new RateLimiter(airconsole);


  airconsole.onMessage = function(from, data) {
    if (data["message_type"] == 0) {
      generateCard(data);
    }
    if (data["message_type"] == 1) {
      receiveNumber(data["number"]);
    }
    if (data["message_type"] == 3) {
      changeScreen(data["screen"]);
    }
    if (data["message_type"] == 6) {
      setGameState(data["state"]);
      if ("autoplay" in data) {
          autoplay = data["autoplay"];
          if (autoplay) {
              tryPlaceToken(cellsDict["FREE"]);
          }
      }
    }
  };

  airconsole.onReady = function(code) {
    viewManager = new AirConsoleViewManager(airconsole);
  };
}

function setGameState(state) {
  gamestate = state;
  if (state == 1 && name != "") {
    changeScreen("gameScreen");
  }
  if (state == 2) {
    changeScreen("gameoverScreen");
  }
}

function changeScreen(screenId) {
  viewManager.show(screenId);
}

function receiveNumber(number) {
  if (activeNumbers.length >= maxActiveNumbers) {
    activeNumbers.shift();
  }
  activeNumbers.push(number);
  if (autoplay && number in cellsDict) {
    cellsDict[number].click();
  }
}

function generateCard(data) {
  for (column = 0; column < 5; column++) {
    for (row = 0; row < 5; row++) {
      var cell = document.createElement("div");
      cell.setAttribute("class", "box");
      cell.setAttribute("id", "r" + row + "c" + column);
      cell.setAttribute("row", row);
      cell.setAttribute("column", column);
      if (column == 2 && row == 2) {
        cell.innerHTML = "FREE";
      } else {
        cell.innerHTML = data["numbers"][row][column];
      }
      numbersDict[cell.id] = data["numbers"][row][column];
      cellsDict[cell.innerHTML] = cell;

      cell.onclick = function() {
        tryPlaceToken(this);
      };
      document.getElementById("card").appendChild(cell);
    }
  }
}

function tryPlaceToken(cell) {
  var row = cell.getAttribute("row");
  var column = cell.getAttribute("column");
  if (((row == 2 && column == 2) || activeNumbers.includes(numbersDict[cell.id])) &&
    !activeTokens[row][column]) {
    cell.style.backgroundColor = "#FF0000";
    activeTokens[row][column] = true;
    checkForBingo(row, column);
  }
}

function checkForBingo(row, column) {
  for (n = 0; n < 5; n++) {
    if (activeTokens[row][n] == false)
      break;
    if (n == 4) {
      setBingoReady();
    }
  }
  for (n = 0; n < 5; n++) {
    if (activeTokens[n][column] == false)
      break;
    if (n == 4) {
      setBingoReady();
    }
  }
  for (n = 0; n < 5; n++) {
    if (activeTokens[n][n] == false)
      break;
    if (n == 4) {
      setBingoReady();
    }
  }
  for (n = 0; n < 5; n++) {
    if (activeTokens[n][4 - n] == false)
      break;
    if (n == 4) {
      setBingoReady();
    }
  }
}

function setBingoReady() {
  document.getElementById("bingoButton").style.display = "block";
  bingoReady = true;
}

function tryCallBingo() {
  if (bingoReady) {
    this.airconsole.message(0, {
      "message_type": 2
    });
  }
}

function changeCellColor(cell) {
  cell.style.backgroundColor = '#' + Math.floor(Math.random() * 16777215).toString(
    16);
}

function submitName() {
  name = document.getElementById("nameInput").value;
  this.airconsole.message(0, {
    "message_type": 3,
    "name": name
  });
  if (gamestate == 0) {
    changeScreen("waitingScreen");
  } else {
    changeScreen("gameScreen");
  }
}
