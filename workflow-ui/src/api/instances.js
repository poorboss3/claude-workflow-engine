import http from './http.js'

export function prepareSubmit(data) {
  return http.post('/process-instances/prepare', data)
}

export function submitProcess(data) {
  return http.post('/process-instances', data)
}
