/*
 *  ExportDefs.h
 *  p1+ITC
 *
 *  Created by Hubert Affolter on 17/Mar/2008.
 *  Copyright 2008 HEKA Elektronik GmbH. All rights reserved.
 *
 */

// ITC_Export is a manifest constant used as a prefix to all function
// prototypes. If a particular prefix is needed (e.g. "PASCAL"), define
// ITC_Export appropriately.
// For Windows DLL builds we need to mark the exported functions as well.
// For MacOS frameworks, define exported symbols.
// For MacOS frameworks, define weak linking.

#ifndef ITC_Export
   #define ITC_DoExport
//   #undef ITC_DoExport

   #ifdef _WINDOWS
      #ifdef ITC_DoExport
         #define ITC_Export __declspec(dllexport) 
         #define ITC_Import  
      #else
         #define ITC_Export  
         #define ITC_Import  
      #endif
   #else
      #ifdef ITC_DoExport
         #define ITC_Export __attribute__((visibility("default"))) 
         #define ITC_Import __attribute__((weak_import))
      #else
         #define ITC_Export  
         #define ITC_Import  
      #endif
   #endif // _WINDOWS
#endif // HEKA_Export

