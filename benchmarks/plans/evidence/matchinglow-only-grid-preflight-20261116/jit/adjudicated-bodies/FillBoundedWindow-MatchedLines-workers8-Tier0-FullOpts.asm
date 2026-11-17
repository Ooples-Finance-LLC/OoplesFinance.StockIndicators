; Assembly listing for method OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:FillBoundedWindow[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+MatchedLinesKernel](OoplesFinance.StockIndicators.Indicators.Bar[],double[][],OoplesFinance.StockIndicators.Indicators.IIndicator,int,System.Threading.CancellationToken,OoplesFinance.StockIndicators.Indicators.OwnedBarBuffer):OoplesFinance.StockIndicators.Indicators.Bar (Tier0-FullOpts)
; Emitting BLENDED_CODE for generic X64 + VEX on Windows
; Tier-0 switched to FullOpts code
; optimized code
; optimized using Synthesized PGO
; rbp based frame
; fully interruptible
; with Synthesized PGO: fgCalledCount is 100
; No PGO data
; 14 inlinees with PGO data; 18 single block inlinees; 13 inlinees without PGO data

G_M000_IG01:
       push     rbp
       push     r15
       push     r14
       push     rdi
       push     rsi
       push     rbx
       sub      rsp, 328
       lea      rbp, [rsp+0x170]
       vxorps   xmm4, xmm4, xmm4
       vmovdqa  xmmword ptr [rbp-0x110], xmm4
       vmovdqa  xmmword ptr [rbp-0x100], xmm4
       mov      rax, -192
       vmovdqa  xmmword ptr [rbp+rax-0x30], xmm4
       vmovdqa  xmmword ptr [rbp+rax-0x20], xmm4
       vmovdqa  xmmword ptr [rbp+rax-0x10], xmm4
       add      rax, 48
       jne      SHORT  -5 instr
       mov      qword ptr [rbp+0x10], rcx
       mov      rbx, rdx
       mov      rsi, r8
       mov      rdi, r9
 
G_M000_IG02:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWSFAST
       mov      r8, rax
       mov      gword ptr [rbp-0x118], r8
       lea      rcx, bword ptr [r8+0x08]
       mov      rdx, rbx
       call     CORINFO_HELP_ASSIGN_REF
       mov      r8, gword ptr [rbp-0x118]
       lea      rcx, bword ptr [r8+0x10]
       mov      rdx, rsi
       call     CORINFO_HELP_ASSIGN_REF
       mov      r8, gword ptr [rbp-0x118]
       lea      rcx, bword ptr [r8+0x18]
       mov      rdx, gword ptr [rbp+0x40]
       call     CORINFO_HELP_ASSIGN_REF
       mov      r8, gword ptr [rbp-0x118]
       mov      ebx, dword ptr [rbp+0x30]
       mov      dword ptr [r8+0x3C], ebx
       lea      rcx, bword ptr [r8+0x40]
       mov      rdx, gword ptr [rbp+0x38]
       call     CORINFO_HELP_ASSIGN_REF
       mov      r8, gword ptr [rbp-0x118]
       lea      rcx, bword ptr [r8+0x28]
       mov      rdx, rdi
       call     CORINFO_HELP_ASSIGN_REF
       mov      r8, gword ptr [rbp-0x118]
       lea      rbx, bword ptr [r8+0x40]
       mov      rcx, rbx
       cmp      gword ptr [rcx], 0
       je       SHORT G_M000_IG04
 
G_M000_IG03:
       mov      rax, gword ptr [rcx]
       cmp      dword ptr [rax+0x20], 0
       jne      G_M000_IG78
 
G_M000_IG04:
       cmp      dword ptr [r8+0x3C], 0x1000
       jle      SHORT G_M000_IG08
 
G_M000_IG05:
       mov      ecx, 1
 
G_M000_IG06:
       mov      dword ptr [r8+0x38], ecx
       xor      ecx, ecx
       mov      dword ptr [rbp-0x2C], ecx
 
G_M000_IG07:
       mov      gword ptr [r8+0x20], rcx
       jmp      SHORT G_M000_IG15
 
G_M000_IG08:
       mov      ecx, dword ptr [(reloc)]
       cmp      ecx, 32
       jl       SHORT G_M000_IG12
 
G_M000_IG09:
       mov      eax, 8
 
G_M000_IG10:
       mov      ecx, eax
 
G_M000_IG11:
       mov      rax, gword ptr [r8+0x08]
       mov      eax, dword ptr [rax+0x08]
       cdq      
       idiv     edx:eax, dword ptr [r8+0x3C]
       cmp      ecx, eax
       cmovg    ecx, eax
       jmp      SHORT G_M000_IG06
 
G_M000_IG12:
       cmp      ecx, 8
       jge      SHORT G_M000_IG09
 
G_M000_IG13:
       mov      eax, ecx
       cmp      eax, 1
       jg       SHORT G_M000_IG10
 
G_M000_IG14:
       mov      ecx, 1
       jmp      SHORT G_M000_IG11
 
G_M000_IG15:
       cmp      dword ptr [r8+0x38], 2
       jl       G_M000_IG52
 
G_M000_IG16:
       mov      rcx, gword ptr [r8+0x08]
       mov      esi, dword ptr [rcx+0x08]
       mov      rcx, gword ptr [r8+0x28]
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:BoundedParallelMinimum(OoplesFinance.StockIndicators.Indicators.IIndicator):int]
       mov      edx, eax
       mov      ecx, esi
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:CanParallelize(int,int):bool]
       test     eax, eax
       je       G_M000_IG52
 
G_M000_IG17:
       test     byte  ptr [(reloc)], 1
       je       G_M000_IG64
 
G_M000_IG18:
       mov      rcx, 0xD1FFAB1E
       mov      rsi, gword ptr [rcx]
       test     rsi, rsi
       je       G_M000_IG65
       mov      rcx, rsi
       xor      edx, edx
       call     System.Threading.Monitor:TryEnter_FastPath_WithTimeout(System.Object,int):int
       cmp      eax, 1
       je       SHORT G_M000_IG21
 
G_M000_IG19:
       test     eax, eax
       je       G_M000_IG52
 
G_M000_IG20:
       mov      rcx, rsi
       xor      edx, edx
       call     [System.Threading.Monitor:TryEnter_Slowpath(System.Object,int):bool]
       test     eax, eax
       je       G_M000_IG52
 
G_M000_IG21:
       mov      dword ptr [rbp-0x2C], 1
       mov      r8, gword ptr [rbp-0x118]
       mov      edx, dword ptr [r8+0x38]
       sub      edx, 1
       jo       G_M000_IG73
       movsxd   rdx, edx
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWARR_1_PTR
       mov      r8, gword ptr [rbp-0x118]
       lea      rcx, bword ptr [r8+0x20]
       mov      rdx, rax
       call     CORINFO_HELP_ASSIGN_REF
       mov      r8, gword ptr [rbp-0x118]
       movsxd   rdx, dword ptr [r8+0x38]
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWARR_1_VC
       mov      r8, gword ptr [rbp-0x118]
       lea      rcx, bword ptr [r8+0x30]
       mov      rdx, rax
       call     CORINFO_HELP_ASSIGN_REF
       xor      esi, esi
       jmp      G_M000_IG28
 
G_M000_IG22:
       mov      edx, dword ptr [r8+0x3C]
       sub      edx, 1
       jo       G_M000_IG73
       mov      rcx, 0xD1FFAB1E
       mov      rcx, gword ptr [rcx]
       call     [System.Buffers.SharedArrayPool`1[OoplesFinance.StockIndicators.Indicators.Bar]:Rent(int):OoplesFinance.StockIndicators.Indicators.Bar[]:this]
 
G_M000_IG23:
       movsxd   rdx, esi
       mov      rcx, r15
       mov      r8, rax
       call     [CORINFO_HELP_ARRADDR_ST]
       mov      r8, gword ptr [rbp-0x118]
       mov      rcx, gword ptr [r8+0x08]
       mov      edx, dword ptr [r8+0x3C]
       sub      r14d, edx
       jo       G_M000_IG73
       add      r14d, 1
       jo       G_M000_IG73
       sub      edx, 1
       jo       G_M000_IG73
       test     rcx, rcx
       jne      G_M000_IG33
 
G_M000_IG24:
       or       edx, r14d
       jne      G_M000_IG69
       xor      rdx, rdx
       xor      eax, eax
 
G_M000_IG25:
       mov      rcx, gword ptr [r8+0x20]
       cmp      esi, dword ptr [rcx+0x08]
       jae      G_M000_IG72
       mov      rcx, gword ptr [rcx+8*rsi+0x10]
       test     rcx, rcx
       je       G_M000_IG34
 
G_M000_IG26:
       lea      r10, bword ptr [rcx+0x10]
       mov      ecx, dword ptr [rcx+0x08]
 
G_M000_IG27:
       cmp      eax, ecx
       jg       G_M000_IG70
       mov      ecx, eax
       imul     r8, rcx, 48
       mov      rcx, r10
       call     [System.SpanHelpers:Memmove(byref,byref,nuint)]
       mov      esi, edi
 
G_M000_IG28:
       mov      r8, gword ptr [rbp-0x118]
       mov      rax, gword ptr [r8+0x20]
       cmp      dword ptr [rax+0x08], esi
       jle      G_M000_IG35
 
G_M000_IG29:
       mov      rcx, rbx
       cmp      gword ptr [rcx], 0
       je       SHORT G_M000_IG31
 
G_M000_IG30:
       mov      rax, gword ptr [rcx]
       cmp      dword ptr [rax+0x20], 0
       jne      G_M000_IG68
 
G_M000_IG31:
       mov      rax, gword ptr [r8+0x08]
       mov      eax, dword ptr [rax+0x08]
       mov      edi, esi
       add      edi, 1
       jo       G_M000_IG73
       movsxd   rdx, edi
       imul     rax, rdx
       jo       G_M000_IG73
       movsxd   rcx, dword ptr [r8+0x38]
       cqo      
       idiv     rdx:rax, rcx
       movsxd   rdx, eax
       cmp      rax, rdx
       jne      G_M000_IG73
       mov      r14d, eax
       mov      r15, gword ptr [r8+0x20]
       cmp      dword ptr [r8+0x3C], 1
       jne      G_M000_IG22
 
G_M000_IG32:
       mov      rax, 0xD1FFAB1E
       jmp      G_M000_IG23
 
G_M000_IG33:
       mov      eax, r14d
       mov      r10d, edx
       add      rax, r10
       mov      r10d, dword ptr [rcx+0x08]
       cmp      rax, r10
       ja       G_M000_IG69
       mov      eax, r14d
       imul     rax, rax, 48
       lea      rcx, bword ptr [rcx+rax+0x10]
       mov      rax, rcx
       mov      ecx, edx
       mov      rdx, rax
       mov      eax, ecx
       jmp      G_M000_IG25
 
G_M000_IG34:
       xor      r10, r10
       xor      ecx, ecx
       jmp      G_M000_IG27
 
G_M000_IG35:
       mov      rcx, 0xD1FFAB1E
       call     CORINFO_HELP_NEWSFAST
       mov      rsi, rax
       mov      r8, gword ptr [rbp-0x118]
       mov      edi, dword ptr [r8+0x38]
       mov      r14d, edi
       lea      rcx, bword ptr [rsi+0x08]
       mov      rdx, r8
       call     CORINFO_HELP_ASSIGN_REF
       mov      rcx, 0xD1FFAB1E
       mov      qword ptr [rsi+0x18], rcx
       mov      rcx, 0xD1FFAB1E
       mov      rcx, gword ptr [rcx]
       cmp      gword ptr [rcx+0x08], 0
       je       G_M000_IG51
 
G_M000_IG36:
       call     [System.Lazy`1[System.__Canon]:CreateValue():System.__Canon:this]
 
G_M000_IG37:
       mov      rcx, rax
       mov      edx, r14d
       mov      r8d, edi
       mov      r9, rsi
       cmp      dword ptr [rcx], ecx
       call     [AiDotNet.Tensors.Helpers.PersistentParallelExecutor:Execute(int,int,System.Action`1[int]):this]
       mov      rcx, rbx
       cmp      gword ptr [rcx], 0
       je       SHORT G_M000_IG39
 
G_M000_IG38:
       mov      rax, gword ptr [rcx]
       cmp      dword ptr [rax+0x20], 0
       jne      G_M000_IG71
 
G_M000_IG39:
       mov      r8, gword ptr [rbp-0x118]
       mov      rbx, gword ptr [r8+0x30]
       mov      esi, dword ptr [rbx+0x08]
       test     esi, esi
       jle      SHORT G_M000_IG45
 
G_M000_IG40:
       add      rbx, 16
 
G_M000_IG41:
       vmovdqu  ymm0, ymmword ptr [rbx]
       vmovdqu  ymmword ptr [rbp-0xF0], ymm0
       vmovdqu  ymm0, ymmword ptr [rbx+0x20]
       vmovdqu  ymmword ptr [rbp-0xD0], ymm0
       mov      rcx, qword ptr [rbx+0x40]
       mov      qword ptr [rbp-0xB0], rcx
       cmp      byte  ptr [rbp-0xC0], 0
       je       SHORT G_M000_IG44
 
G_M000_IG42:
       test     byte  ptr [(reloc)], 1
       je       G_M000_IG74
 
G_M000_IG43:
       mov      rcx, 0xD1FFAB1E
       mov      rcx, gword ptr [rcx]
       lea      rdx, [rbp-0xF0]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Validation.IndicatorInputDomain:Validate(byref):this]
 
G_M000_IG44:
       add      rbx, 72
       dec      esi
       jne      SHORT G_M000_IG41
 
G_M000_IG45:
       mov      r8, gword ptr [rbp-0x118]
       mov      rbx, gword ptr [r8+0x30]
       mov      esi, dword ptr [rbx+0x08]
       test     esi, esi
       jle      SHORT G_M000_IG50
 
G_M000_IG46:
       add      rbx, 16
 
G_M000_IG47:
       mov      r8, gword ptr [rbp-0x118]
       mov      edx, dword ptr [rbx+0x34]
       vmovsd   xmm3, qword ptr [rbx+0x38]
       mov      rcx, gword ptr [r8+0x28]
       test     edx, edx
       jl       SHORT G_M000_IG49
 
G_M000_IG48:
       mov      r8d, edx
       xor      edx, edx
       call     [OoplesFinance.StockIndicators.Validation.IndicatorOutputPolicy:Validate(OoplesFinance.StockIndicators.Indicators.IIndicator,int,int,double)]
 
G_M000_IG49:
       add      rbx, 72
       dec      esi
       jne      SHORT G_M000_IG47
 
G_M000_IG50:
       mov      r8, gword ptr [rbp-0x118]
       mov      rcx, gword ptr [r8+0x30]
       mov      eax, dword ptr [rcx+0x08]
       dec      eax
       cmp      eax, dword ptr [rcx+0x08]
       jae      G_M000_IG72
       lea      rax, [rax+8*rax]
       vmovdqu  ymm0, ymmword ptr [rcx+8*rax+0x10]
       vmovdqu  ymmword ptr [rbp-0xA8], ymm0
       vmovdqu  xmm0, xmmword ptr [rcx+8*rax+0x30]
       vmovdqu  xmmword ptr [rbp-0x88], xmm0
       jmp      G_M000_IG75
 
G_M000_IG51:
       mov      rax, gword ptr [rcx+0x18]
       jmp      G_M000_IG37
 
G_M000_IG52:
       mov      r8, gword ptr [rbp-0x118]
       mov      rcx, gword ptr [r8+0x08]
       mov      rdx, rcx
       mov      r9, gword ptr [r8+0x10]
       mov      rax, gword ptr [r8+0x18]
       mov      ecx, dword ptr [rcx+0x08]
       mov      r10d, dword ptr [r8+0x3C]
       xor      r11d, r11d
       mov      dword ptr [rsp+0x20], r11d
       mov      dword ptr [rsp+0x28], ecx
       mov      dword ptr [rsp+0x30], r10d
 
G_M000_IG53:
       mov      bword ptr [rbp-0x100], r11
 
G_M000_IG54:
       mov      dword ptr [rbp-0xF8], r11d
 
G_M000_IG55:
       mov      bword ptr [rbp-0x110], r11
 
G_M000_IG56:
       mov      dword ptr [rbp-0x108], r11d
       mov      rcx, gword ptr [r8+0x40]
       mov      gword ptr [rsp+0x48], rcx
       mov      rcx, gword ptr [r8+0x28]
       mov      gword ptr [rsp+0x50], rcx
       lea      rcx, [rbp-0x100]
       mov      qword ptr [rsp+0x38], rcx
       lea      rcx, [rbp-0x110]
       mov      qword ptr [rsp+0x40], rcx
       lea      rcx, [rbp-0x78]
       mov      r8, r9
       mov      r9, rax
       call     [OoplesFinance.StockIndicators.Indicators.ValuesBarExecution:ComputeBoundedRegion[OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+MatchedLinesKernel](OoplesFinance.StockIndicators.Indicators.Bar[],double[][],OoplesFinance.StockIndicators.Indicators.OwnedBarBuffer,int,int,int,System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.ReadOnlySpan`1[OoplesFinance.StockIndicators.Indicators.Bar],System.Threading.CancellationToken,OoplesFinance.StockIndicators.Indicators.IIndicator):OoplesFinance.StockIndicators.Indicators.ValuesBarExecution+PointwiseRegion]
       mov      rcx, rbx
       cmp      gword ptr [rcx], 0
       je       SHORT G_M000_IG58
 
G_M000_IG57:
       mov      rax, gword ptr [rcx]
       cmp      dword ptr [rax+0x20], 0
       jne      G_M000_IG66
 
G_M000_IG58:
       cmp      byte  ptr [rbp-0x48], 0
       je       SHORT G_M000_IG61
 
G_M000_IG59:
       test     byte  ptr [(reloc)], 1
       je       G_M000_IG67
 
G_M000_IG60:
       mov      rcx, 0xD1FFAB1E
       mov      rcx, gword ptr [rcx]
       lea      rdx, [rbp-0x78]
       cmp      dword ptr [rcx], ecx
       call     [OoplesFinance.StockIndicators.Validation.IndicatorInputDomain:Validate(byref):this]
 
G_M000_IG61:
       mov      r8, gword ptr [rbp-0x118]
       mov      rcx, gword ptr [r8+0x28]
       cmp      dword ptr [rbp-0x44], 0
       jl       SHORT G_M000_IG63
 
G_M000_IG62:
       vmovsd   xmm3, qword ptr [rbp-0x40]
       mov      r8d, dword ptr [rbp-0x44]
       xor      edx, edx
       call     [OoplesFinance.StockIndicators.Validation.IndicatorOutputPolicy:Validate(OoplesFinance.StockIndicators.Indicators.IIndicator,int,int,double)]
 
G_M000_IG63:
       vmovdqu  ymm0, ymmword ptr [rbp-0x78]
       vmovdqu  ymmword ptr [rbp-0xA8], ymm0
       vmovdqu  xmm0, xmmword ptr [rbp-0x58]
       vmovdqu  xmmword ptr [rbp-0x88], xmm0
       jmp      SHORT G_M000_IG75
 
G_M000_IG64:
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_GET_GCSTATIC_BASE]
       jmp      G_M000_IG18
 
G_M000_IG65:
       xor      rcx, rcx
       call     [System.ArgumentNullException:Throw(System.String)]
       int3     
 
G_M000_IG66:
       call     [System.Threading.CancellationToken:ThrowOperationCanceledException():this]
       int3     
 
G_M000_IG67:
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_GET_GCSTATIC_BASE]
       jmp      G_M000_IG60
 
G_M000_IG68:
       call     [System.Threading.CancellationToken:ThrowOperationCanceledException():this]
       int3     
 
G_M000_IG69:
       call     [System.ThrowHelper:ThrowArgumentOutOfRangeException()]
       int3     
 
G_M000_IG70:
       call     [System.ThrowHelper:ThrowArgumentException_DestinationTooShort()]
       int3     
 
G_M000_IG71:
       call     [System.Threading.CancellationToken:ThrowOperationCanceledException():this]
       int3     
 
G_M000_IG72:
       call     CORINFO_HELP_RNGCHKFAIL
       int3     
 
G_M000_IG73:
       call     CORINFO_HELP_OVERFLOW
       int3     
 
G_M000_IG74:
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_GET_GCSTATIC_BASE]
       jmp      G_M000_IG43
 
G_M000_IG75:
       call     G_M000_IG79
       nop      
 
G_M000_IG76:
       mov      rax, qword ptr [rbp+0x10]
       vmovdqu  ymm0, ymmword ptr [rbp-0xA8]
       vmovdqu  ymmword ptr [rax], ymm0
       vmovdqu  xmm0, xmmword ptr [rbp-0x88]
       vmovdqu  xmmword ptr [rax+0x20], xmm0
 
G_M000_IG77:
       vzeroupper 
       add      rsp, 328
       pop      rbx
       pop      rsi
       pop      rdi
       pop      r14
       pop      r15
       pop      rbp
       ret      
 
G_M000_IG78:
       call     [System.Threading.CancellationToken:ThrowOperationCanceledException():this]
       int3     
 
G_M000_IG79:
       sub      rsp, 88
 
G_M000_IG80:
       cmp      dword ptr [rbp-0x2C], 0
       je       SHORT G_M000_IG87
 
G_M000_IG81:
       test     byte  ptr [(reloc)], 1
       jne      SHORT G_M000_IG83
 
G_M000_IG82:
       mov      rcx, 0xD1FFAB1E
       call     [CORINFO_HELP_GET_GCSTATIC_BASE]
 
G_M000_IG83:
       mov      rcx, 0xD1FFAB1E
       mov      rbx, gword ptr [rcx]
       test     rbx, rbx
       jne      SHORT G_M000_IG85
 
G_M000_IG84:
       xor      rcx, rcx
       call     [System.ArgumentNullException:Throw(System.String)]
       int3     
 
G_M000_IG85:
       mov      rcx, rbx
       call     System.Threading.Monitor:Exit_FastPath(System.Object):int
       test     eax, eax
       je       SHORT G_M000_IG87
 
G_M000_IG86:
       mov      ecx, eax
       mov      rdx, rbx
       call     [System.Threading.Monitor:Exit_Slowpath(int,System.Object)]
 
G_M000_IG87:
       mov      r8, gword ptr [rbp-0x118]
       cmp      gword ptr [r8+0x20], 0
       je       SHORT G_M000_IG93
 
G_M000_IG88:
       mov      rbx, gword ptr [r8+0x20]
       xor      esi, esi
       cmp      dword ptr [rbx+0x08], 0
       jle      SHORT G_M000_IG93
 
G_M000_IG89:
       mov      ecx, esi
       mov      rdx, gword ptr [rbx+8*rcx+0x10]
       test     rdx, rdx
       je       SHORT G_M000_IG92
 
G_M000_IG90:
       cmp      dword ptr [rdx+0x08], 0
       jle      SHORT G_M000_IG92
 
G_M000_IG91:
       mov      rcx, 0xD1FFAB1E
       mov      rcx, gword ptr [rcx]
       xor      r8d, r8d
       call     [System.Buffers.SharedArrayPool`1[OoplesFinance.StockIndicators.Indicators.Bar]:Return(OoplesFinance.StockIndicators.Indicators.Bar[],bool):this]
 
G_M000_IG92:
       inc      esi
       cmp      dword ptr [rbx+0x08], esi
       jg       SHORT G_M000_IG89
 
G_M000_IG93:
       vzeroupper 
       add      rsp, 88
       ret      
 
; Total bytes of code 1914