/**
 * EventBus Utility
 * 
 * Simple pub/sub pattern implementation for decoupled cross-component communication.
 */
export const EventBus = {
  /** @type {Object.<string, Array<Function>>} */
  listeners: {},

  /**
   * Subscribe to an event
   * 
   * @param {string} event - The event name
   * @param {Function} callback - The callback function to execute
   */
  on(event, callback) {
    if (!this.listeners[event]) this.listeners[event] = [];
    this.listeners[event].push(callback);
  },

  /**
   * Publish an event with optional data
   * 
   * @param {string} event - The event name
   * @param {*} data - The payload to pass to subscribers
   */
  emit(event, data) {
    if (this.listeners[event]) {
      this.listeners[event].forEach(cb => cb(data));
    }
  },

  /**
   * Unsubscribe from an event
   * 
   * @param {string} event - The event name
   * @param {Function} callback - The callback function to remove
   */
  off(event, callback) {
    if (!this.listeners[event]) return;
    this.listeners[event] = this.listeners[event].filter(cb => cb !== callback);
  }
};
