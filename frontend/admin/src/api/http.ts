import axios from 'axios'

const http = axios.create({
  baseURL: '/api/v1',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' }
})

http.interceptors.response.use(
  res => res,
  err => {
    console.error('API Error:', err)
    return Promise.reject(err)
  }
)

export default http
