const express = require('express')
const app = express()
const port = 80

app.get('/', (req, res) => {
  res.send('Hello World!')
})

app.get('/uppercase', (req, res) => {
    console.log(`Uppercase Service receieved request: ${JSON.stringify(req.query)}`)
    var result = {
        original: req.query['sentence'],
        sentence: req.query['sentence'].toUpperCase()
    };
    res.send(result);
  })

app.listen(port, () => {
  console.log(`Uppercase Service listening at http://localhost:${port}`)
})