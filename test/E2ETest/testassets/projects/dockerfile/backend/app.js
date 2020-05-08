var express = require('express');
var http = require('http');
var app = express();

app.get('/', function (req, res) {
  res.send('Hello World!');
});
http.createServer(app).listen(80, function () {
  console.log('Example app listening on port 80!');
});