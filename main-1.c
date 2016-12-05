//
//  main.c
//  read_iso
//
//  Copyright (c) 2016 Jinga-hi, Inc. All rights reserved.
//

#include <libusb-1.0/libusb.h>
#include <signal.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/time.h>

// Nordic RF USB dongle.
#define VENDOR_ID 0x1915
#define PRODUCT_ID 0x7B
// Isochronous interface.
#define INTERFACE 1
#define ISO_PACKETS 1000
#define PACKET_LENGTH 160
#define ENDPOINT_ADDRESS 0x88
// Status report settings
#define DO_STATUS_REPORT 1
#define STATUS_REPORT_INTERVAL 10000

// Static prototypes (defined below).
static void list_devices(libusb_context *context);
static void cleanup(libusb_context **context_ptr,
                    libusb_device_handle **handle_ptr,
                    int interface_number,
                    struct libusb_transfer **transfer_ptr);
static void LIBUSB_CALL callback_fn(struct libusb_transfer *transfer);
static void timer_cb(int signal);

// Global variable
int packets_received = 0;
int completed = 0;
volatile sig_atomic_t capture_data = 1;  // TODO: Allow user to set to false through signal or otherwise.
int seconds_capture = 100;  // 0 for unlimited, otherwise seconds to capture.

int main(int argc, const char * argv[])
{
    int rc;
    libusb_context *context;
    libusb_device_handle *handle = NULL;
    struct libusb_transfer *transfer = NULL;
    
    rc = libusb_init(&context);
    if (rc != 0) {
        printf("libusb_init failed.\n");
        return 1;
    }
    //libusb_set_debug(context, LIBUSB_LOG_LEVEL_DEBUG);
    list_devices(context);
    
    printf("Opening device.\n");
    handle = libusb_open_device_with_vid_pid(context, VENDOR_ID, PRODUCT_ID);
    if (handle == NULL) {
        printf("ERROR: Could not open device with VID %d PID %d not connected.\n",
               VENDOR_ID, PRODUCT_ID);
        cleanup(&context, &handle, INTERFACE, &transfer);
        exit(1);
    }

// Claim interface #1  
    printf("Claiming interface.\n");
    if (libusb_claim_interface(handle, INTERFACE) != 0) {
        printf("ERROR: Could not claim interface %d\n", INTERFACE);
        cleanup(&context, &handle, INTERFACE, &transfer);
        exit(1);
    }
    
	// Allocate transfer
    printf("Allocating transfer.\n");
    transfer = libusb_alloc_transfer(ISO_PACKETS);
    if (transfer == NULL) {
        printf("ERROR: Transfer not allocated.\n");
        cleanup(&context, &handle, INTERFACE, &transfer);
        exit(1);
    }
    
    // transfer->flags = LIBUSB_TRANSFER_SHORT_NOT_OK;   // Consider enabling, if .
    unsigned char buffer[ISO_PACKETS * PACKET_LENGTH];	// 160,000
	
    libusb_fill_iso_transfer(transfer, handle,
							ENDPOINT_ADDRESS,
							buffer,
                            sizeof(buffer),		// Buffer size is 160,000
							ISO_PACKETS,		// Number of ISO packets = 1000
							callback_fn,		// Callback
                            NULL,				// User data
							ISO_PACKETS * 2);	// Timeout = 2000
							
    libusb_set_iso_packet_lengths(transfer,
                                  libusb_get_max_iso_packet_size(libusb_get_device(handle),
								  ENDPOINT_ADDRESS));
    struct timeval tv;
    tv.tv_sec  = 2;     // seconds
    tv.tv_usec = 0;     // microseconds
    
    int bytes_transferred = 0;
    int total_bytes = 0;
    FILE *fptr = fopen("/tmp/data_dump", "wb");
    
    
    if (seconds_capture > 0) {  // Set up timer and signal handler.
        struct sigaction sa;
        struct itimerval timer_val;
        struct timeval interval;
        
        memset(&sa, 0, sizeof(sa));
        sa.sa_handler = timer_cb;
        sigaction(SIGALRM, &sa, NULL);
        
        interval.tv_sec = seconds_capture;
        interval.tv_usec = 0;
        timer_val.it_interval = interval;
        timer_val.it_value = interval;
        setitimer(ITIMER_REAL, &timer_val, NULL);  // Will trigger SIGALRM when done.
    }
    
    while (capture_data == 1) {  // Main loop.
        completed = 0;
        
        rc = libusb_submit_transfer(transfer);
        if (rc != 0) {
            printf("ERROR: Transfer submit failed, error code %d\n", rc);
            cleanup(&context, &handle, INTERFACE, &transfer);
            exit(1);
        }
    
        // Wait for submitted transfer to complete.
        while (!completed) {
            libusb_handle_events_timeout_completed(context, &tv, NULL);
        }

        for (int i = 0; i < ISO_PACKETS; i++) {
            bytes_transferred = transfer->iso_packet_desc[i].actual_length;
            fwrite(libusb_get_iso_packet_buffer_simple(transfer, i), bytes_transferred, 1, fptr);
            if (bytes_transferred > 0) {  // Only count data-bearing packets.
              total_bytes += bytes_transferred;
              ++packets_received;
            }
        }
#ifdef DO_STATUS_REPORT
        if (packets_received % STATUS_REPORT_INTERVAL == 0) {  // Status report
            printf("Packets: %d, Bytes: %d, Buffer: %02x %02x %02x\n", packets_received,
                   total_bytes,
                   transfer->buffer[0], transfer->buffer[1], transfer->buffer[2]);
        }
#endif
    }
    
    fflush(fptr);
    fclose(fptr);
    if (libusb_release_interface(handle, INTERFACE) != 0) {
        printf("ERROR: Could not release interface %d\n", INTERFACE);
        cleanup(&context, &handle, INTERFACE, &transfer);
        exit(1);
    }
    
    cleanup(&context, &handle, INTERFACE, &transfer);
    return 0;
}

// Static functions.

static void cleanup(libusb_context **context_ptr,
                    libusb_device_handle **handle_ptr,
                    int interface_number,
                    struct libusb_transfer **transfer_ptr) {
    if (*transfer_ptr != NULL) {
        printf("Freeing transfer.\n");
        libusb_free_transfer(*transfer_ptr);
    }
    if (*handle_ptr != NULL) {
        printf("Releasing interface.\n");
        libusb_release_interface(*handle_ptr, interface_number);
        printf("Closing handle.\n");
        libusb_close(*handle_ptr);
    }

// This tends to cause crashes. Why? Does closing the handle invalidate the context?
//    if (*context_ptr != NULL) {
//        printf("Exiting context.\n");
//        libusb_exit(*context_ptr);
//    }
    
}

static void list_devices(libusb_context *context) {
    ssize_t num_devices;
    struct libusb_device_descriptor device_descriptor;
    libusb_device **device_list;
    libusb_device *device;

    num_devices = libusb_get_device_list(context, &device_list);
    printf("\nConnected devices\n");
    for (int i = 0; i < num_devices; i++) {
        device = device_list[i];
        libusb_get_device_descriptor(device, &device_descriptor);
        printf("Vendor ID: %d, Product ID: %d\n", device_descriptor.idVendor,
               device_descriptor.idProduct);
    }
    printf("\n");
    libusb_free_device_list(device_list, 0);
}

static void timer_cb(int signal) {
    printf("Timer expired.\n");
    capture_data = 0;  // Stop capturing data and exit.
}

static void LIBUSB_CALL callback_fn(struct libusb_transfer *transfer) {
    // Only set the completed flag. The rest of the work is done in the main loop.
    completed = 1;
}