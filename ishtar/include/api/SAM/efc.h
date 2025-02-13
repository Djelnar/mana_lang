/**
 * \file
 *
 * \brief Embedded Flash Controller (EFC) driver for SAM.
 *
 * Copyright (c) 2011-2012 Atmel Corporation. All rights reserved.
 *
 * \asf_license_start
 *
 * \page License
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice,
 *    this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 *    this list of conditions and the following disclaimer in the documentation
 *    and/or other materials provided with the distribution.
 *
 * 3. The name of Atmel may not be used to endorse or promote products derived
 *    from this software without specific prior written permission.
 *
 * 4. This software may only be redistributed and used in connection with an
 *    Atmel microcontroller product.
 *
 * THIS SOFTWARE IS PROVIDED BY ATMEL "AS IS" AND ANY EXPRESS OR IMPLIED
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT ARE
 * EXPRESSLY AND SPECIFICALLY DISCLAIMED. IN NO EVENT SHALL ATMEL BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
 * OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
 * HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
 * ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 *
 * \asf_license_stop
 *
 */

#ifndef EFC_H_INCLUDED
#define EFC_H_INCLUDED

#include <Arduino.h>
#include <inttypes.h>

#define SAM3XA
#define RAMFUNC __attribute__ ((section(".ramfunc")))

/// @cond 0
/**INDENT-OFF**/
#ifdef __cplusplus
extern "C" {
#endif
/**INDENT-ON**/
/// @endcond

/*! \name EFC return codes */
//! @{
typedef enum efc_rc {
	EFC_RC_OK = 0,      //!< Operation OK
	EFC_RC_YES = 0,     //!< Yes
	EFC_RC_NO = 1,      //!< No
	EFC_RC_ERROR = 1,   //!< General error
	EFC_RC_INVALID,     //!< Invalid argument input
	EFC_RC_NOT_SUPPORT = 0xFFFFFFFF //!< Operation is not supported
} efc_rc_t;
//! @}

/*! \name EFC command */
//! @{
#define EFC_FCMD_GETD    0x00  //!< Get Flash Descriptor
#define EFC_FCMD_WP      0x01  //!< Write page
#define EFC_FCMD_WPL     0x02  //!< Write page and lock
#define EFC_FCMD_EWP     0x03  //!< Erase page and write page
#define EFC_FCMD_EWPL    0x04  //!< Erase page and write page then lock
#define EFC_FCMD_EA      0x05  //!< Erase all
#if (SAM3SD8)
#define EFC_FCMD_EPL     0x06  //!< Erase plane
#endif
#if (SAM4S || SAM4E)
#define EFC_FCMD_EPA     0x07  //!< Erase pages
#endif
#define EFC_FCMD_SLB     0x08  //!< Set Lock Bit
#define EFC_FCMD_CLB     0x09  //!< Clear Lock Bit
#define EFC_FCMD_GLB     0x0A  //!< Get Lock Bit
#define EFC_FCMD_SGPB    0x0B  //!< Set GPNVM Bit
#define EFC_FCMD_CGPB    0x0C  //!< Clear GPNVM Bit
#define EFC_FCMD_GGPB    0x0D  //!< Get GPNVM Bit
#define EFC_FCMD_STUI    0x0E  //!< Start unique ID
#define EFC_FCMD_SPUI    0x0F  //!< Stop unique ID
#if (!SAM3U && !SAM3SD8 && !SAM3S8)
#define EFC_FCMD_GCALB   0x10  //!< Get CALIB Bit
#endif
#if (SAM4S || SAM4E)
#define EFC_FCMD_ES      0x11  //!< Erase sector
#define EFC_FCMD_WUS     0x12  //!< Write user signature
#define EFC_FCMD_EUS     0x13  //!< Erase user signature
#define EFC_FCMD_STUS    0x14  //!< Start read user signature
#define EFC_FCMD_SPUS    0x15  //!< Stop read user signature
#endif
//! @}

/*! The IAP function entry address */
#define CHIP_FLASH_IAP_ADDRESS  (IROM_ADDR + 8)

/*! \name EFC access mode */
//! @{
#define EFC_ACCESS_MODE_128    0
#define EFC_ACCESS_MODE_64     EEFC_FMR_FAM
//! @}

uint32_t efc_init(Efc *p_efc, uint32_t ul_access_mode, uint32_t ul_fws);
void efc_enable_frdy_interrupt(Efc *p_efc);
void efc_disable_frdy_interrupt(Efc *p_efc);
void efc_set_flash_access_mode(Efc *p_efc, uint32_t ul_mode);
uint32_t efc_get_flash_access_mode(Efc *p_efc);
void efc_set_wait_state(Efc *p_efc, uint32_t ul_fws);
uint32_t efc_get_wait_state(Efc *p_efc);
uint32_t efc_perform_command(Efc *p_efc, uint32_t ul_command,
		uint32_t ul_argument);
uint32_t efc_get_status(Efc *p_efc);
uint32_t efc_get_result(Efc *p_efc);
uint32_t efc_perform_read_sequence(Efc *p_efc,
		uint32_t ul_cmd_st, uint32_t ul_cmd_sp,
		uint32_t *p_ul_buf, uint32_t ul_size);

/// @cond 0
/**INDENT-OFF**/
#ifdef __cplusplus
}
#endif
/**INDENT-ON**/
/// @endcond


/// @cond 0
/**INDENT-OFF**/
#ifdef __cplusplus
extern "C" {
#endif
/**INDENT-ON**/
/// @endcond

/**
 * \defgroup sam_drivers_efc_group Enhanced Embedded Flash Controller (EEFC)
 *
 * The Enhanced Embedded Flash Controller ensures the interface of the Flash
 * block with the 32-bit internal bus.
 *
 * @{
 */

/* Address definition for read operation */
# define READ_BUFF_ADDR0    IFLASH0_ADDR
# define READ_BUFF_ADDR1    IFLASH1_ADDR

/* Flash Writing Protection Key */
#define FWP_KEY    0x5Au

#if (SAM4S || SAM4E)
#define EEFC_FCR_FCMD(value) \
	((EEFC_FCR_FCMD_Msk & ((value) << EEFC_FCR_FCMD_Pos)))
#define EEFC_ERROR_FLAGS  (EEFC_FSR_FLOCKE | EEFC_FSR_FCMDE | EEFC_FSR_FLERR)
#else
#define EEFC_ERROR_FLAGS  (EEFC_FSR_FLOCKE | EEFC_FSR_FCMDE)
#endif



/*
 * Local function declaration.
 * Because they are RAM functions, they need 'extern' declaration.
 */
extern void efc_write_fmr(Efc *p_efc, uint32_t ul_fmr);
extern uint32_t efc_perform_fcr(Efc *p_efc, uint32_t ul_fcr);

/**
 * \brief Initialize the EFC controller.
 *
 * \param ul_access_mode 0 for 128-bit, EEFC_FMR_FAM for 64-bit.
 * \param ul_fws The number of wait states in cycle (no shift).
 *
 * \return 0 if successful.
 */
uint32_t efc_init(Efc *p_efc, uint32_t ul_access_mode, uint32_t ul_fws)
{
	efc_write_fmr(p_efc, ul_access_mode | EEFC_FMR_FWS(ul_fws));
	return EFC_RC_OK;
}

/**
 * \brief Enable the flash ready interrupt.
 *
 * \param p_efc Pointer to an EFC instance.
 */
void efc_enable_frdy_interrupt(Efc *p_efc)
{
	uint32_t ul_fmr = p_efc->EEFC_FMR;

	efc_write_fmr(p_efc, ul_fmr | EEFC_FMR_FRDY);
}

/**
 * \brief Disable the flash ready interrupt.
 *
 * \param p_efc Pointer to an EFC instance.
 */
void efc_disable_frdy_interrupt(Efc *p_efc)
{
	uint32_t ul_fmr = p_efc->EEFC_FMR;

	efc_write_fmr(p_efc, ul_fmr & (~EEFC_FMR_FRDY));
}

/**
 * \brief Set flash access mode.
 *
 * \param p_efc Pointer to an EFC instance.
 * \param ul_mode 0 for 128-bit, EEFC_FMR_FAM for 64-bit.
 */
void efc_set_flash_access_mode(Efc *p_efc, uint32_t ul_mode)
{
	uint32_t ul_fmr = p_efc->EEFC_FMR & (~EEFC_FMR_FAM);

	efc_write_fmr(p_efc, ul_fmr | ul_mode);
}

/**
 * \brief Get flash access mode.
 *
 * \param p_efc Pointer to an EFC instance.
 *
 * \return 0 for 128-bit or EEFC_FMR_FAM for 64-bit.
 */
uint32_t efc_get_flash_access_mode(Efc *p_efc)
{
	return (p_efc->EEFC_FMR & EEFC_FMR_FAM);
}

/**
 * \brief Set flash wait state.
 *
 * \param p_efc Pointer to an EFC instance.
 * \param ul_fws The number of wait states in cycle (no shift).
 */
void efc_set_wait_state(Efc *p_efc, uint32_t ul_fws)
{
	uint32_t ul_fmr = p_efc->EEFC_FMR & (~EEFC_FMR_FWS_Msk);

	efc_write_fmr(p_efc, ul_fmr | EEFC_FMR_FWS(ul_fws));
}

/**
 * \brief Get flash wait state.
 *
 * \param p_efc Pointer to an EFC instance.
 *
 * \return The number of wait states in cycle (no shift).
 */
uint32_t efc_get_wait_state(Efc *p_efc)
{
	return ((p_efc->EEFC_FMR & EEFC_FMR_FWS_Msk) >> EEFC_FMR_FWS_Pos);
}

/**
 * \brief Perform the given command and wait until its completion (or an error).
 *
 * \note Unique ID commands are not supported, use efc_read_unique_id.
 *
 * \param p_efc Pointer to an EFC instance.
 * \param ul_command Command to perform.
 * \param ul_argument Optional command argument.
 *
 * \note This function will automatically choose to use IAP function.
 *
 * \return 0 if successful, otherwise returns an error code.
 */
uint32_t efc_perform_command(Efc *p_efc, uint32_t ul_command,
		uint32_t ul_argument)
{
	/* Unique ID commands are not supported. */
	if (ul_command == EFC_FCMD_STUI || ul_command == EFC_FCMD_SPUI) {
		return EFC_RC_NOT_SUPPORT;
	}

	/* Use IAP function with 2 parameters in ROM. */
	static uint32_t(*iap_perform_command) (uint32_t, uint32_t);
	uint32_t ul_efc_nb = (p_efc == EFC0) ? 0 : 1;

	iap_perform_command =
			(uint32_t(*)(uint32_t, uint32_t))
			*((uint32_t *) CHIP_FLASH_IAP_ADDRESS);
	iap_perform_command(ul_efc_nb,
			EEFC_FCR_FKEY(FWP_KEY) | EEFC_FCR_FARG(ul_argument) |
			EEFC_FCR_FCMD(ul_command));
	return (p_efc->EEFC_FSR & EEFC_ERROR_FLAGS);
}

/**
 * \brief Get the current status of the EEFC.
 *
 * \note This function clears the value of some status bits (FLOCKE, FCMDE).
 *
 * \param p_efc Pointer to an EFC instance.
 *
 * \return The current status.
 */
uint32_t efc_get_status(Efc *p_efc)
{
	return p_efc->EEFC_FSR;
}

/**
 * \brief Get the result of the last executed command.
 *
 * \param p_efc Pointer to an EFC instance.
 *
 * \return The result of the last executed command.
 */
uint32_t efc_get_result(Efc *p_efc)
{
	return p_efc->EEFC_FRR;
}

/**
 * \brief Perform read sequence. Supported sequences are read Unique ID and
 * read User Signature
 *
 * \param p_efc Pointer to an EFC instance.
 * \param ul_cmd_st Start command to perform.
 * \param ul_cmd_sp Stop command to perform.
 * \param p_ul_buf Pointer to an data buffer.
 * \param ul_size Buffer size.
 *
 * \return 0 if successful, otherwise returns an error code.
 */
RAMFUNC
uint32_t efc_perform_read_sequence(Efc *p_efc,
		uint32_t ul_cmd_st, uint32_t ul_cmd_sp,
		uint32_t *p_ul_buf, uint32_t ul_size)
{
	volatile uint32_t ul_status;
	uint32_t ul_cnt;

	uint32_t *p_ul_data =
			(uint32_t *) ((p_efc == EFC0) ?
			READ_BUFF_ADDR0 : READ_BUFF_ADDR1);

	if (p_ul_buf == NULL) {
		return EFC_RC_INVALID;
	}

	p_efc->EEFC_FMR |= (0x1u << 16);

	/* Send the Start Read command */

	p_efc->EEFC_FCR = EEFC_FCR_FKEY(FWP_KEY) | EEFC_FCR_FARG(0)
			| EEFC_FCR_FCMD(ul_cmd_st);
	/* Wait for the FRDY bit in the Flash Programming Status Register
	 * (EEFC_FSR) falls.
	 */
	do {
		ul_status = p_efc->EEFC_FSR;
	} while ((ul_status & EEFC_FSR_FRDY) == EEFC_FSR_FRDY);

	/* The data is located in the first address of the Flash
	 * memory mapping.
	 */
	for (ul_cnt = 0; ul_cnt < ul_size; ul_cnt++) {
		p_ul_buf[ul_cnt] = p_ul_data[ul_cnt];
	}

	/* To stop the read mode */
	p_efc->EEFC_FCR =
			EEFC_FCR_FKEY(FWP_KEY) | EEFC_FCR_FARG(0) |
			EEFC_FCR_FCMD(ul_cmd_sp);
	/* Wait for the FRDY bit in the Flash Programming Status Register (EEFC_FSR)
	 * rises.
	 */
	do {
		ul_status = p_efc->EEFC_FSR;
	} while ((ul_status & EEFC_FSR_FRDY) != EEFC_FSR_FRDY);

	p_efc->EEFC_FMR &= ~(0x1u << 16);

	return EFC_RC_OK;
}

/**
 * \brief Set mode register.
 *
 * \param p_efc Pointer to an EFC instance.
 * \param ul_fmr Value of mode register
 */
RAMFUNC
void efc_write_fmr(Efc *p_efc, uint32_t ul_fmr)
{
	p_efc->EEFC_FMR = ul_fmr;
}

/**
 * \brief Perform command.
 *
 * \param p_efc Pointer to an EFC instance.
 * \param ul_fcr Flash command.
 *
 * \return The current status.
 */
RAMFUNC
uint32_t efc_perform_fcr(Efc *p_efc, uint32_t ul_fcr)
{
	volatile uint32_t ul_status;

	p_efc->EEFC_FCR = ul_fcr;
	do {
		ul_status = p_efc->EEFC_FSR;
	} while ((ul_status & EEFC_FSR_FRDY) != EEFC_FSR_FRDY);

	return (ul_status & EEFC_ERROR_FLAGS);
}

//@}

/// @cond 0
/**INDENT-OFF**/
#ifdef __cplusplus
}
#endif
/**INDENT-ON**/
/// @endcond

#endif /* EFC_H_INCLUDED */