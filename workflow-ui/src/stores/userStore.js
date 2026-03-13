import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useUserStore = defineStore('user', () => {
  const userId = ref(localStorage.getItem('workflow_userId') || 'user1')

  function setUserId(id) {
    userId.value = id
    localStorage.setItem('workflow_userId', id)
  }

  return { userId, setUserId }
})
