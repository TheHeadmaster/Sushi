includelib ucrt.lib
includelib legacy_stdio_definitions.lib
includelib msvcrt.lib

option casemap:none

.data
; , 10 means line feed character (LF)
; , 0 means adding an terminating '\0' to the string
fmtStr byte 'Hello', 10, 0

.code
externdef printf:proc
externdef _CRT_INIT:proc
externdef exit:proc

main proc
    call _CRT_INIT
    push rbp
    mov rbp, rsp

    sub rsp, 32
    lea rcx, fmtStr  ; lea: load the address of a variable into a register
    call printf

    xor ecx, ecx ; the first argument for exit() is setting to 0
    call exit
main endp

end