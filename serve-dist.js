const http = require('http')
const fs = require('fs')
const path = require('path')
const PORT = 5176
const DIST = path.join(__dirname, 'web', 'dist')
const server = http.createServer((req, res) => {
  let url = req.url.split('?')[0]
  let filePath = path.join(DIST, url === '/' ? 'index.html' : url)
  const exists = fs.existsSync(filePath)
  if (!exists) filePath = path.join(DIST, 'index.html')
  const ext = path.extname(filePath)
  const types = { '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css' }
  res.writeHead(200, { 'Content-Type': types[ext] || 'text/plain' })
  res.end(fs.readFileSync(filePath))
})
server.listen(PORT, () => console.log('ready on ' + PORT))
