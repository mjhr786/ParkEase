/**
 * Mock Razorpay SDK for development and testing.
 * Mimics the behavior of the real Razorpay checkout script.
 */
class MockRazorpay {
    constructor(options) {
        this.options = options;
    }

    open() {
        console.log('Mock Razorpay Checkout Opened', this.options);

        // Simulate user interaction with a delay
        setTimeout(() => {
            const isSuccess = window.confirm(
                `Mock Payment Gateway\n\n` +
                `Payable Amount: ₹${this.options.amount / 100}\n` +
                `Order ID: ${this.options.order_id}\n\n` +
                `Click OK to simulate SUCCESS.\n` +
                `Click Cancel to simulate FAILURE.`
            );

            if (isSuccess) {
                const response = {
                    razorpay_payment_id: `pay_mock_${Date.now()}`,
                    razorpay_order_id: this.options.order_id,
                    razorpay_signature: "mock_signature_valid"
                };

                console.log('Payment Success', response);
                if (this.options.handler) {
                    this.options.handler(response);
                }
            } else {
                const error = {
                    code: "BAD_REQUEST_ERROR",
                    description: "Payment processing cancelled by user",
                    source: "customer",
                    step: "payment_authentication",
                    reason: "payment_cancelled",
                    metadata: {
                        order_id: this.options.order_id,
                        payment_id: null
                    }
                };

                console.log('Payment Failed', error);
                // Razorpay doesn't always have a strict error handler in the options (often it's 'modal.ondismiss')
                // But typically for standard checkout, it just closes. 
                // We'll simulate a toast error here or call a retry mechanism if defined.
                alert("Payment Failed/Cancelled");
            }
        }, 500);
    }
}

export default MockRazorpay;
